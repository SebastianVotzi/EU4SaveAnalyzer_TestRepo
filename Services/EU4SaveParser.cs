using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using EU4SaveAnalyzer.Models;

namespace EU4SaveAnalyzer.Services;

/// <summary>
/// Verarbeitet EU4 Save-Dateien und extrahiert alle Länderdaten, Kriege und Statistiken.
/// EU4 1.35+ speichert Länderdaten direkt als Root-Level Keys (z.B. FRA={...}).
/// Inactive Länder (~500) haben nur statische Daten → fehlende Werte werden aus
/// Provinzdaten berechnet (analog zu PDX Tools / Skanderbeg).
/// </summary>
public class EU4SaveParser
{
    private static readonly HashSet<string> _ignoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "REB", "PIR", "NAT", "OOO", "---", "SYN", "TUR", "INS", "RAN", "UNK"
    };

    private static readonly Regex _tagPattern = new(@"^[A-Z]{3}$", RegexOptions.Compiled);

    /// <summary>
    /// EU4 mil_tech → Force Limit Bonus. Index = Tech-Level.
    /// Quelle: EU4 technologies.txt
    /// </summary>
    private static readonly int[] MilTechFLBonus =
    {
        0, 0, 2, 2, 3, 3, 3, 3, 3, 3,
        5, 3, 3, 5, 3, 5, 3, 5, 3, 5,
        3, 5, 3, 5, 3, 5, 5, 5, 5, 5, 5, 5, 5
    };

    // ============================================================
    // Öffentliche API
    // ============================================================

    public async Task<(SaveGame save, List<Country> countries, List<War> wars)>
        ParseAsync(Stream fileStream, string fileName)
    {
        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;

        byte[] hdr = new byte[6];
        ms.Read(hdr, 0, 6);
        ms.Position = 0;

        string gameDate  = "";
        string playerTag = "";
        string rawText;

        if (hdr[0] == 0x50 && hdr[1] == 0x4B) // ZIP
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);

            // meta-Datei enthält das aktuelle Spieldatum (nicht Startdatum)
            var metaEntry = zip.GetEntry("meta");
            if (metaEntry != null)
            {
                await using var metaStream = metaEntry.Open();
                using var metaReader = new StreamReader(metaStream, Encoding.Latin1);
                string metaText = await metaReader.ReadToEndAsync();
                metaText = StripHeader(metaText);
                var metaRoot = new ClausewitzParser(metaText).Parse();
                gameDate  = ClausewitzParser.GetString(metaRoot, "date");
                playerTag = ClausewitzParser.GetString(metaRoot, "player").Trim('"');
            }

            ms.Position = 0;
            rawText = await ExtractFromZip(ms);
        }
        else if (hdr[0] == 0x45 && hdr[1] == 0x55 && hdr[2] == 0x34 && hdr[3] == 0x62)
        {
            throw new InvalidDataException(
                "Ironman-Saves werden nicht unterstützt. Bitte ohne Ironman-Modus starten.");
        }
        else
        {
            ms.Position = 0;
            using var reader = new StreamReader(ms, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);
            rawText = StripHeader(await reader.ReadToEndAsync());
        }

        var root            = new ClausewitzParser(rawText).Parse();
        var playerNameByTag = ParsePlayerNames(root);

        if (string.IsNullOrEmpty(gameDate))
            gameDate = ClausewitzParser.GetString(root, "date", "unbekannt");
        if (string.IsNullOrEmpty(playerTag))
        {
            playerTag = ClausewitzParser.GetString(root, "player").Trim('"');
            if (string.IsNullOrEmpty(playerTag) && playerNameByTag.Count > 0)
                playerTag = playerNameByTag.Keys.First();
        }

        var save = new SaveGame
        {
            FileName   = fileName,
            GameDate   = gameDate,
            PlayerTag  = playerTag,
            UploadedAt = DateTime.UtcNow
        };

        var countries = ParseCountries(root, playerNameByTag);
        var wars      = ParseWars(root);

        return (save, countries, wars);
    }

    // ============================================================
    // Format-Erkennung
    // ============================================================

    private async Task<string> ExtractFromZip(MemoryStream ms)
    {
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
        var entry = zip.GetEntry("gamestate")
                    ?? zip.Entries.FirstOrDefault(e => e.Name.EndsWith(".eu4", StringComparison.OrdinalIgnoreCase))
                    ?? zip.Entries.First();
        await using var es = entry.Open();
        using var sr = new StreamReader(es, Encoding.Latin1);
        return StripHeader(await sr.ReadToEndAsync());
    }

    private static string StripHeader(string text)
    {
        if (text.StartsWith("EU4txt", StringComparison.OrdinalIgnoreCase))
        {
            int nl = text.IndexOf('\n');
            if (nl >= 0) return text[(nl + 1)..];
        }
        return text;
    }

    // ============================================================
    // Spieler-Mapping
    // ============================================================

    private Dictionary<string, string> ParsePlayerNames(Dictionary<string, object> root)
    {
        var map    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var values = ClausewitzParser.GetBareValues(root, "players_countries");
        for (int i = 0; i + 1 < values.Count; i += 2)
        {
            string name = values[i].Trim('"');
            string tag  = values[i + 1].Trim('"').ToUpperInvariant();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(tag))
                map[tag] = name;
        }
        return map;
    }

    // ============================================================
    // Länder parsen
    // ============================================================

    private List<Country> ParseCountries(
        Dictionary<string, object> root,
        Dictionary<string, string> playerNameByTag)
    {
        var result = new List<Country>();

        foreach (var kv in root)
        {
            string tag = kv.Key.ToUpperInvariant();
            if (!_tagPattern.IsMatch(tag) || _ignoredTags.Contains(tag)) continue;

            // Doppelte Tags → größtes Dictionary nehmen (vollständigste Daten)
            Dictionary<string, object>? cData = null;
            if (kv.Value is Dictionary<string, object> d)
                cData = d;
            else if (kv.Value is List<object> lst)
                cData = lst.OfType<Dictionary<string, object>>()
                            .OrderByDescending(x => x.Count)
                            .FirstOrDefault();

            if (cData == null || !IsCountryBlock(cData)) continue;

            var country = BuildCountry(tag, cData, playerNameByTag, root);
            if (country != null) result.Add(country);
        }

        // Fallback: altes countries={} Format
        if (result.Count == 0)
        {
            var cn = ClausewitzParser.GetDict(root, "countries");
            if (cn != null)
            {
                foreach (var kv in cn)
                {
                    string tag = kv.Key.ToUpperInvariant();
                    if (tag == "_VALUES" || _ignoredTags.Contains(tag)) continue;
                    if (kv.Value is not Dictionary<string, object> cData) continue;
                    var country = BuildCountry(tag, cData, playerNameByTag, root);
                    if (country != null) result.Add(country);
                }
            }
        }

        // Provinz-Scan: berechnet fehlende Werte für inactive Länder aus Provinzdaten
        var provinceStats = ScanProvinces(root);
        foreach (var country in result)
        {
            if (!provinceStats.TryGetValue(country.Tag, out var ps)) continue;

            if (ps.ProvinceCount > 0)
                country.ProvinceCount = ps.ProvinceCount;
            if (ps.TotalDevelopment > country.TotalDevelopment)
                country.TotalDevelopment = ps.TotalDevelopment;

            // Dev Clicks aus Provinz-Scan (deckt alle Provinzen ab)
            if (ps.DevClicksAdm + ps.DevClicksDip + ps.DevClicksMil >
                country.DevClicksAdm + country.DevClicksDip + country.DevClicksMil)
            {
                country.DevClicksAdm = ps.DevClicksAdm;
                country.DevClicksDip = ps.DevClicksDip;
                country.DevClicksMil = ps.DevClicksMil;
            }

            // Manpower aus Provinzen wenn nicht im Länder-Block (inactive Länder)
            if (country.MaxManpower == 0 && ps.MaxManpower > 0)
                country.MaxManpower = Math.Round(ps.MaxManpower, 2);
            if (country.Manpower == 0 && ps.MaxManpower > 0)
                country.Manpower = Math.Round(ps.MaxManpower * 0.8, 2);

            // Einkommen aus Provinzdaten wenn kein Ledger vorhanden
            if (country.MonthlyIncome == 0 && ps.ProvinceCount > 0)
            {
                country.IncomeTax        = Math.Round(ps.TaxIncome, 2);
                country.IncomeProduction = Math.Round(ps.ProdIncome, 2);
                country.IncomeTrade      = Math.Round(ps.TaxIncome * 0.3, 2);
                country.MonthlyIncome    = Math.Round(ps.TaxIncome + ps.ProdIncome + country.IncomeTrade, 2);
            }
        }

        return result
            .OrderByDescending(c => c.IsHuman)
            .ThenByDescending(c => c.TotalDevelopment)
            .ToList();
    }

    private static bool IsCountryBlock(Dictionary<string, object> data)
    {
        return data.ContainsKey("treasury")
            || data.ContainsKey("technology")
            || data.ContainsKey("capital")
            || data.ContainsKey("stability")
            || data.ContainsKey("government");
    }

    private Country? BuildCountry(
        string tag,
        Dictionary<string, object> cData,
        Dictionary<string, string> playerNameByTag,
        Dictionary<string, object> root)
    {
        bool   isHuman  = ClausewitzParser.GetString(cData, "human") == "yes";
        int    provinces = ClausewitzParser.GetInt(cData, "num_of_cities");
        int    rawDev   = (int)ClausewitzParser.GetDouble(cData, "raw_development");
        double treasury = ClausewitzParser.GetDouble(cData, "treasury");

        bool hasTerritory = provinces > 0 || rawDev > 0 || Math.Abs(treasury) > 0.01;
        if (!hasTerritory && !isHuman) return null;

        var country = new Country
        {
            Tag              = tag,
            IsHuman          = isHuman,
            ProvinceCount    = provinces,
            TotalDevelopment = rawDev > 0 ? rawDev : (int)ClausewitzParser.GetDouble(cData, "development"),
        };

        // Name: aus Save-File oder aus Wörterbuch
        country.Name = ParseCountryName(cData, tag);
        if (country.Name == tag)
            country.Name = EU4TagNames.GetName(tag);

        if (isHuman)
        {
            playerNameByTag.TryGetValue(tag, out string? name);
            country.PlayerName = name ?? tag;
        }

        ParseEconomy(cData, country);
        ParseMilitary(cData, country);
        ParseMana(cData, country);
        ParseRuler(cData, country);
        ParseDevClicksFromProvinces(root, cData, country);

        return country;
    }

    private static string ParseCountryName(Dictionary<string, object> cData, string fallback)
    {
        string name = ClausewitzParser.GetString(cData, "name");
        if (!string.IsNullOrEmpty(name)) return name;

        var history = ClausewitzParser.GetDict(cData, "history");
        if (history != null)
        {
            name = ClausewitzParser.GetString(history, "name");
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return fallback;
    }

    // ============================================================
    // Wirtschaft
    // ============================================================

    private void ParseEconomy(Dictionary<string, object> cData, Country country)
    {
        country.Treasury = ClausewitzParser.GetDouble(cData, "treasury");

        var ledger = ClausewitzParser.GetDict(cData, "ledger");
        if (ledger != null)
        {
            country.MonthlyIncome   = ClausewitzParser.GetDouble(ledger, "lastmonthincome");
            country.MonthlyExpenses = ClausewitzParser.GetDouble(ledger, "lastmonthexpense");

            var incTable = ClausewitzParser.GetDict(ledger, "lastmonthincometable");
            var expTable = ClausewitzParser.GetDict(ledger, "lastmonthexpensetable");

            var incValues = GetValuesAsList(incTable);
            var expValues = GetValuesAsList(expTable);

            if (incValues.Count > 0)
            {
                country.IncomeTax        = GetDoubleAtIndex(incValues, 0);
                country.IncomeProduction = GetDoubleAtIndex(incValues, 1);
                country.IncomeTrade      = GetDoubleAtIndex(incValues, 2);
                country.IncomeGold       = GetDoubleAtIndex(incValues, 3);
                country.IncomeTribute    = GetDoubleAtIndex(incValues, 4) + GetDoubleAtIndex(incValues, 5);
            }
            else if (country.MonthlyIncome > 0)
            {
                country.IncomeTax        = country.MonthlyIncome * 0.35;
                country.IncomeProduction = country.MonthlyIncome * 0.28;
                country.IncomeTrade      = country.MonthlyIncome * 0.27;
                country.IncomeGold       = country.MonthlyIncome * 0.10;
            }

            if (expValues.Count > 0)
            {
                country.ExpenseArmy      = GetDoubleAtIndex(expValues, 0);
                country.ExpenseNavy      = GetDoubleAtIndex(expValues, 1);
                country.ExpenseBuildings = GetDoubleAtIndex(expValues, 2);
                double totalExp = expValues.Sum();
                country.ExpenseOther = Math.Max(0,
                    totalExp - country.ExpenseArmy - country.ExpenseNavy - country.ExpenseBuildings);
            }
            else if (country.MonthlyExpenses > 0)
            {
                country.ExpenseArmy      = country.MonthlyExpenses * 0.50;
                country.ExpenseNavy      = country.MonthlyExpenses * 0.15;
                country.ExpenseBuildings = country.MonthlyExpenses * 0.20;
                country.ExpenseOther     = country.MonthlyExpenses * 0.15;
            }
        }
        else
        {
            country.MonthlyIncome   = ClausewitzParser.GetDouble(cData, "estimated_monthly_income");
            country.MonthlyExpenses = ClausewitzParser.GetDouble(cData, "estimated_monthly_expenses");
        }
    }

    // ============================================================
    // Militär
    // ============================================================

    private void ParseMilitary(Dictionary<string, object> cData, Country country)
    {
        country.Manpower    = ClausewitzParser.GetDouble(cData, "manpower");
        country.MaxManpower = ClausewitzParser.GetDouble(cData, "max_manpower");

        int armySize = 0, navySize = 0;

        if (cData.TryGetValue("army", out var armyObj))
            foreach (var a in NormalizeToList(armyObj).OfType<Dictionary<string, object>>())
                if (a.TryGetValue("regiment", out var regObj))
                    armySize += NormalizeToList(regObj).Count;

        if (cData.TryGetValue("navy", out var navyObj))
            foreach (var n in NormalizeToList(navyObj).OfType<Dictionary<string, object>>())
                if (n.TryGetValue("ship", out var shipObj))
                    navySize += NormalizeToList(shipObj).Count;

        // Force Limit: Basis(10) + Σ tech-Boni + dev/10
        int forceLimit = 0, navalFL = 0;
        var tech = ClausewitzParser.GetDict(cData, "technology");
        if (tech != null)
        {
            int milTech   = ClausewitzParser.GetInt(tech, "mil_tech");
            int navalTech = ClausewitzParser.GetInt(tech, "naval_tech");
            int techFL    = 10;
            for (int t = 1; t <= Math.Min(milTech, MilTechFLBonus.Length - 1); t++)
                techFL += MilTechFLBonus[t];
            forceLimit = techFL + Math.Max(0, country.TotalDevelopment / 10);
            navalFL    = Math.Max(3, country.TotalDevelopment / 15 + navalTech / 2);
        }
        if (forceLimit == 0) forceLimit = Math.Max(5, 10 + country.TotalDevelopment / 10);
        if (navalFL    == 0) navalFL    = Math.Max(3, country.TotalDevelopment / 20);

        country.ForceLimit      = forceLimit;
        country.NavalForceLimit = navalFL;
        country.ArmySize        = armySize > 0 ? armySize : (int)(forceLimit * 0.75);
        country.NavySize        = navySize;
    }

    // ============================================================
    // Mana
    // ============================================================

    private void ParseMana(Dictionary<string, object> cData, Country country)
    {
        // Aktuelles Mana aus powers-Block (_values = [adm, dip, mil])
        var powers = ClausewitzParser.GetDict(cData, "powers");
        if (powers != null)
        {
            var vals = GetValuesAsList(powers);
            if (vals.Count >= 3)
            {
                country.AdmPower = (int)vals[0];
                country.DipPower = (int)vals[1];
                country.MilPower = (int)vals[2];
            }
            else
            {
                country.AdmPower = ClausewitzParser.GetInt(powers, "adm");
                country.DipPower = ClausewitzParser.GetInt(powers, "dip");
                country.MilPower = ClausewitzParser.GetInt(powers, "mil");
            }
        }

        var admSpent = ClausewitzParser.GetDict(cData, "adm_spent_indexed");
        var dipSpent = ClausewitzParser.GetDict(cData, "dip_spent_indexed");
        var milSpent = ClausewitzParser.GetDict(cData, "mil_spent_indexed");

        int totalAdm = ClausewitzParser.SumDictValues(admSpent);
        int totalDip = ClausewitzParser.SumDictValues(dipSpent);
        int totalMil = ClausewitzParser.SumDictValues(milSpent);

        country.TotalAdmGenerated = totalAdm + country.AdmPower;
        country.TotalDipGenerated = totalDip + country.DipPower;
        country.TotalMilGenerated = totalMil + country.MilPower;

        // ADM: 0=Stabilität, 1=Tech, 2=Ideen, 7=Dev
        int admDev = ClausewitzParser.SumIndices(admSpent, 7);
        country.AdmSpentStability = ClausewitzParser.SumIndices(admSpent, 0);
        country.AdmSpentTech      = ClausewitzParser.SumIndices(admSpent, 1);
        country.AdmSpentIdeas     = ClausewitzParser.SumIndices(admSpent, 2);
        country.AdmSpentOther     = Math.Max(0,
            totalAdm - country.AdmSpentStability - country.AdmSpentTech - country.AdmSpentIdeas - admDev);

        // DIP: 1=Tech, 2=Ideen, 3-6=Diplomatie, 7=Dev
        int dipDev = ClausewitzParser.SumIndices(dipSpent, 7);
        country.DipSpentTech      = ClausewitzParser.SumIndices(dipSpent, 1);
        country.DipSpentIdeas     = ClausewitzParser.SumIndices(dipSpent, 2);
        country.DipSpentDiplomacy = ClausewitzParser.SumIndices(dipSpent, 3, 4, 5, 6);
        country.DipSpentOther     = Math.Max(0,
            totalDip - country.DipSpentTech - country.DipSpentIdeas - country.DipSpentDiplomacy - dipDev);

        // MIL: 0=Tech, 1=Ideen, 4-5=Generale, 7=Dev
        int milDev = ClausewitzParser.SumIndices(milSpent, 7);
        country.MilSpentTech    = ClausewitzParser.SumIndices(milSpent, 0);
        country.MilSpentIdeas   = ClausewitzParser.SumIndices(milSpent, 1);
        country.MilSpentLeaders = ClausewitzParser.SumIndices(milSpent, 4, 5);
        country.MilSpentOther   = Math.Max(0,
            totalMil - country.MilSpentTech - country.MilSpentIdeas - country.MilSpentLeaders - milDev);
    }

    // ============================================================
    // Herrscher
    // ============================================================

    private void ParseRuler(Dictionary<string, object> cData, Country country)
    {
        var history = ClausewitzParser.GetDict(cData, "history");
        if (history == null) return;

        Dictionary<string, object>? latestMonarch = null;
        foreach (var kv in history)
        {
            if (kv.Value is not Dictionary<string, object> entry) continue;
            var m = ClausewitzParser.GetDict(entry, "monarch");
            if (m != null && m.ContainsKey("adm"))
                latestMonarch = m;
        }

        if (latestMonarch != null)
        {
            country.RulerAdm = ClausewitzParser.GetInt(latestMonarch, "adm");
            country.RulerDip = ClausewitzParser.GetInt(latestMonarch, "dip");
            country.RulerMil = ClausewitzParser.GetInt(latestMonarch, "mil");
        }
    }

    // ============================================================
    // Dev Clicks aus Provinzen
    // ============================================================

    private void ParseDevClicksFromProvinces(
        Dictionary<string, object> root,
        Dictionary<string, object> cData,
        Country country)
    {
        var ownedBlock = ClausewitzParser.GetDict(cData, "owned_provinces");
        if (ownedBlock == null) return;
        if (!ownedBlock.TryGetValue("_values", out var vObj) || vObj is not List<string> provIds) return;

        int admClicks = 0, dipClicks = 0, milClicks = 0;
        foreach (string idStr in provIds)
        {
            if (!root.TryGetValue("-" + idStr.Trim(), out var provObj)) continue;
            if (provObj is not Dictionary<string, object> prov) continue;

            int curTax  = (int)ClausewitzParser.GetDouble(prov, "base_tax");
            int curProd = (int)ClausewitzParser.GetDouble(prov, "base_production");
            int curMp   = (int)ClausewitzParser.GetDouble(prov, "base_manpower");

            var hist    = ClausewitzParser.GetDict(prov, "history");
            int initTax  = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_tax",  curTax)  : curTax;
            int initProd = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_production", curProd) : curProd;
            int initMp   = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_manpower",  curMp)  : curMp;
            if (initTax == curTax) initTax = (int)ClausewitzParser.GetDouble(prov, "original_tax", curTax);

            admClicks += Math.Max(0, curTax  - initTax);
            dipClicks += Math.Max(0, curProd - initProd);
            milClicks += Math.Max(0, curMp   - initMp);
        }
        country.DevClicksAdm = admClicks;
        country.DevClicksDip = dipClicks;
        country.DevClicksMil = milClicks;
    }

    // ============================================================
    // Provinz-Scan: aggregiert Daten aller Provinzen pro Besitzer
    // ============================================================

    private Dictionary<string, ProvinceStats> ScanProvinces(Dictionary<string, object> root)
    {
        var stats = new Dictionary<string, ProvinceStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in root)
        {
            if (!kv.Key.StartsWith("-")) continue;
            if (!int.TryParse(kv.Key, out _)) continue;
            if (kv.Value is not Dictionary<string, object> prov) continue;

            string owner = ClausewitzParser.GetString(prov, "owner");
            if (string.IsNullOrEmpty(owner)) continue;

            int tax  = (int)ClausewitzParser.GetDouble(prov, "base_tax");
            int prod = (int)ClausewitzParser.GetDouble(prov, "base_production");
            int mp   = (int)ClausewitzParser.GetDouble(prov, "base_manpower");

            var hist    = ClausewitzParser.GetDict(prov, "history");
            int initTax  = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_tax",  tax)  : tax;
            int initProd = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_production", prod) : prod;
            int initMp   = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_manpower", mp) : mp;
            if (initTax == tax) initTax = (int)ClausewitzParser.GetDouble(prov, "original_tax", tax);

            if (!stats.ContainsKey(owner)) stats[owner] = new ProvinceStats();
            var s = stats[owner];
            s.ProvinceCount++;
            s.TotalDevelopment += tax + prod + mp;
            s.TaxIncome        += tax / 12.0;
            s.ProdIncome       += prod * 0.2;
            s.MaxManpower      += mp;
            s.DevClicksAdm     += Math.Max(0, tax  - initTax);
            s.DevClicksDip     += Math.Max(0, prod - initProd);
            s.DevClicksMil     += Math.Max(0, mp   - initMp);
            stats[owner] = s;
        }
        return stats;
    }

    private class ProvinceStats
    {
        public int    ProvinceCount    { get; set; }
        public int    TotalDevelopment { get; set; }
        public double TaxIncome        { get; set; }
        public double ProdIncome       { get; set; }
        public double MaxManpower      { get; set; }
        public int    DevClicksAdm     { get; set; }
        public int    DevClicksDip     { get; set; }
        public int    DevClicksMil     { get; set; }
    }

    // ============================================================
    // Kriege
    // ============================================================

    private List<War> ParseWars(Dictionary<string, object> root)
    {
        var result = new List<War>();
        if (root.TryGetValue("active_war", out var aw))
            foreach (var w in NormalizeToList(aw).OfType<Dictionary<string, object>>())
                result.Add(ParseSingleWar(w, true));
        if (root.TryGetValue("previous_war", out var pw))
            foreach (var w in NormalizeToList(pw).OfType<Dictionary<string, object>>())
                result.Add(ParseSingleWar(w, false));
        return result;
    }

    private War ParseSingleWar(Dictionary<string, object> warData, bool isActive)
    {
        var war = new War
        {
            IsActive  = isActive,
            Name      = ClausewitzParser.GetString(warData, "name"),
            WarScore  = ClausewitzParser.GetDouble(warData, "superiority"),
            Outcome   = isActive ? "ongoing" : "unknown"
        };

        var history = ClausewitzParser.GetDict(warData, "history");
        if (history != null)
        {
            var dateKeys = history.Keys
                .Where(k => k.Length >= 8 && k.Contains('.') && char.IsDigit(k[0]))
                .OrderBy(k => k).ToList();
            if (dateKeys.Count > 0)   war.StartDate = dateKeys.First();
            if (!isActive && dateKeys.Count > 1) war.EndDate = dateKeys.Last();
        }

        if (!isActive)
        {
            war.Outcome = ClausewitzParser.GetDouble(warData, "superiority") switch
            {
                > 0  => "attacker_win",
                < 0  => "defender_win",
                _    => "draw"
            };
        }

        var attackers = new List<string>();
        var defenders = new List<string>();

        string origAtt = ClausewitzParser.GetString(warData, "original_attacker");
        string origDef = ClausewitzParser.GetString(warData, "original_defender");
        if (!string.IsNullOrEmpty(origAtt)) attackers.Add(origAtt);
        if (!string.IsNullOrEmpty(origDef)) defenders.Add(origDef);

        if (warData.TryGetValue("persistent_attackers", out var paObj) &&
            paObj is Dictionary<string, object> paDict)
        {
            var vals = GetValuesAsList(paDict);
            attackers.AddRange(vals.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v)));
        }
        if (warData.TryGetValue("persistent_defenders", out var pdObj) &&
            pdObj is Dictionary<string, object> pdDict)
        {
            var vals = GetValuesAsList(pdDict);
            defenders.AddRange(vals.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v)));
        }

        war.AttackerTags = string.Join(", ", attackers.Distinct().Where(t => !string.IsNullOrEmpty(t)));
        war.DefenderTags = string.Join(", ", defenders.Distinct().Where(t => !string.IsNullOrEmpty(t)));

        ParseWarLosses(warData, war);
        return war;
    }

    private void ParseWarLosses(Dictionary<string, object> warData, War war)
    {
        var history = ClausewitzParser.GetDict(warData, "history");
        if (history == null) return;
        int attTotal = 0, defTotal = 0;
        foreach (var kv in history)
        {
            if (kv.Value is not Dictionary<string, object> evt) continue;
            if (!evt.TryGetValue("battle", out var battleObj)) continue;
            foreach (var b in NormalizeToList(battleObj).OfType<Dictionary<string, object>>())
            {
                var att = ClausewitzParser.GetDict(b, "attacker");
                var def = ClausewitzParser.GetDict(b, "defender");
                if (att != null) attTotal += ClausewitzParser.GetInt(att, "losses");
                if (def != null) defTotal += ClausewitzParser.GetInt(def, "losses");
            }
        }
        war.AttackerLosses = attTotal;
        war.DefenderLosses = defTotal;
    }

    // ============================================================
    // Hilfsmethoden
    // ============================================================

    private List<object> NormalizeToList(object obj)
    {
        if (obj is List<object> l) return l;
        if (obj is Dictionary<string, object> d) return new List<object> { d };
        return new List<object>();
    }

    private static List<double> GetValuesAsList(Dictionary<string, object>? dict)
    {
        if (dict == null) return new List<double>();
        if (!dict.TryGetValue("_values", out var raw)) return new List<double>();
        if (raw is List<string> ls)
            return ls.Select(s => double.TryParse(s,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d) ? d : 0.0).ToList();
        return new List<double>();
    }

    private static double GetDoubleAtIndex(List<double> list, int index)
        => index < list.Count ? list[index] : 0.0;
}
