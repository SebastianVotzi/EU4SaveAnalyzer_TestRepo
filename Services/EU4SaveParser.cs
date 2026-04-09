using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using EU4SaveAnalyzer.Models;

namespace EU4SaveAnalyzer.Services;

/// <summary>
/// Verarbeitet EU4 Save-Dateien (.eu4) und extrahiert alle relevanten Daten.
/// Unterstützt EU4 1.35+ Format wo Länderdaten direkt als Root-Level Keys stehen.
/// </summary>
public class EU4SaveParser
{
    private static readonly HashSet<string> _ignoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "REB", "PIR", "NAT", "OOO", "---", "SYN", "TUR", "INS", "RAN", "UNK"
    };

    private static readonly Regex _tagPattern = new(@"^[A-Z]{3}$", RegexOptions.Compiled);

    // ============================================================
    // Öffentliche API
    // ============================================================

    public async Task<(SaveGame save, List<Country> countries, List<War> wars)>
        ParseAsync(Stream fileStream, string fileName)
    {
        // Gesamten Stream in MemoryStream lesen (für ZIP-Zugriff auf mehrere Einträge)
        var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms);
        ms.Position = 0;

        byte[] hdr = new byte[6];
        ms.Read(hdr, 0, 6);
        ms.Position = 0;

        string gameDate = "";
        string playerTag = "";
        string rawText;

        if (hdr[0] == 0x50 && hdr[1] == 0x4B) // ZIP-Format
        {
            // meta-Datei lesen: enthält aktuelles Spieldatum und Spieler-Tag
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);

            var metaEntry = zip.GetEntry("meta");
            if (metaEntry != null)
            {
                await using var metaStream = metaEntry.Open();
                using var metaReader = new StreamReader(metaStream, Encoding.Latin1);
                string metaText = await metaReader.ReadToEndAsync();
                if (metaText.StartsWith("EU4txt", StringComparison.OrdinalIgnoreCase))
                {
                    int nl = metaText.IndexOf('
'); if (nl >= 0) metaText = metaText[(nl+1)..]; }

                var metaRoot = new ClausewitzParser(metaText).Parse();
                    // meta hat immer das aktuelle Spieldatum
                    gameDate = ClausewitzParser.GetString(metaRoot, "date");
                    playerTag = ClausewitzParser.GetString(metaRoot, "player").Trim('"');
                }

                ms.Position = 0;
                rawText = await ExtractFromZip(ms);
            }
            else
            {
                // Unkomprimierter Save: direkt lesen
                ms.Position = 0;
                using var reader = new StreamReader(ms, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);
                rawText = StripHeader(await reader.ReadToEndAsync());
            }

            var root = new ClausewitzParser(rawText).Parse();
            var playerNameByTag = ParsePlayerNames(root);

            // Fallback: Datum und Spieler aus gamestate wenn meta nicht verfügbar
            if (string.IsNullOrEmpty(gameDate))
                gameDate = ParseGameDate(root);
            if (string.IsNullOrEmpty(playerTag))
            {
                playerTag = ClausewitzParser.GetString(root, "player").Trim('"');
                if (string.IsNullOrEmpty(playerTag) && playerNameByTag.Count > 0)
                    playerTag = playerNameByTag.Keys.First();
            }

            var save = new SaveGame
            {
                FileName = fileName,
                GameDate = gameDate,
                PlayerTag = playerTag,
                UploadedAt = DateTime.UtcNow
            };

            var countries = ParseCountries(root, playerNameByTag);
            var wars = ParseWars(root);

            return (save, countries, wars);
        }

        // ============================================================
        // Format-Erkennung
        // ============================================================

        private async Task<string> ReadSaveTextAsync(Stream stream)
        {
            var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            byte[] header = new byte[6];
            int bytesRead = ms.Read(header, 0, 6);
            ms.Position = 0;

            if (bytesRead >= 2 && header[0] == 0x50 && header[1] == 0x4B)
                return await ExtractFromZip(ms);

            if (bytesRead >= 6 && header[0] == 0x45 && header[1] == 0x55 &&
                header[2] == 0x34 && header[3] == 0x62)
                throw new InvalidDataException(
                    "Ironman-Saves werden nicht unterstützt. Bitte ohne Ironman-Modus starten.");

            ms.Position = 0;
            using var reader = new StreamReader(ms, Encoding.Latin1, detectEncodingFromByteOrderMarks: true);
            return StripHeader(await reader.ReadToEndAsync());
        }

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
        // Spieler-Mapping & Datum
        // ============================================================

        private Dictionary<string, string> ParsePlayerNames(Dictionary<string, object> root)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var values = ClausewitzParser.GetBareValues(root, "players_countries");
            for (int i = 0; i + 1 < values.Count; i += 2)
            {
                string playerName = values[i].Trim('"');
                string tag = values[i + 1].Trim('"').ToUpperInvariant();
                if (!string.IsNullOrEmpty(playerName) && !string.IsNullOrEmpty(tag))
                    map[tag] = playerName;
            }
            return map;
        }

        /// <summary>
        /// Liest das Spieldatum. EU4 speichert es als "date=1444.11.11" im Root.
        /// Bei manchen Saves steht es nicht direkt als Root-Key sondern weiter hinten.
        /// </summary>
        private static string ParseGameDate(Dictionary<string, object> root)
        {
            string date = ClausewitzParser.GetString(root, "date");
            if (!string.IsNullOrEmpty(date)) return date;

            // Fallback: in start_date suchen
            return ClausewitzParser.GetString(root, "start_date", "unbekannt");
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
                if (!_tagPattern.IsMatch(tag)) continue;
                if (_ignoredTags.Contains(tag)) continue;

                // EU4 kann denselben Tag mehrfach im Save haben → List<object>
                // Wir nehmen das Dictionary mit den MEISTEN Keys (= vollständigste Daten)
                // Der erste Eintrag ist oft nur ein Stub mit minimalen Daten,
                // der zweite enthält die vollen Länderdaten (Armee, Einkommen usw.)
                Dictionary<string, object>? cData = null;
                if (kv.Value is Dictionary<string, object> d)
                    cData = d;
                else if (kv.Value is List<object> lst)
                    cData = lst.OfType<Dictionary<string, object>>()
                                .OrderByDescending(x => x.Count)
                                .FirstOrDefault();

                if (cData == null) continue;
                if (!IsCountryBlock(cData)) continue;

                var country = BuildCountry(tag, cData, playerNameByTag, root);
                if (country != null)
                    result.Add(country);
            }

            // Fallback: altes countries={} Format
            if (result.Count == 0)
            {
                var countriesNode = ClausewitzParser.GetDict(root, "countries");
                if (countriesNode != null)
                {
                    foreach (var kv in countriesNode)
                    {
                        string tag = kv.Key.ToUpperInvariant();
                        if (tag == "_VALUES" || _ignoredTags.Contains(tag)) continue;
                        if (kv.Value is not Dictionary<string, object> cData) continue;
                        var country = BuildCountry(tag, cData, playerNameByTag, root);
                        if (country != null) result.Add(country);
                    }
                }
            }

            // Provinz-Scan: verlässliche Daten für ALLE Länder aus Provinzdaten
            var provinceStats = ScanProvinces(root);
            foreach (var country in result)
            {
                if (!provinceStats.TryGetValue(country.Tag, out var ps)) continue;

                // ProvinceCount und TotalDevelopment aus Provinzdaten überschreiben
                // (zuverlässiger als die gecachten Werte im Länder-Block)
                if (ps.ProvinceCount > 0)
                    country.ProvinceCount = ps.ProvinceCount;

                if (ps.TotalDevelopment > country.TotalDevelopment)
                    country.TotalDevelopment = ps.TotalDevelopment;

                // DevClicks aus Provinzdaten (aus ParseDevClicksFromProvinces bereits gesetzt,
                // aber Provinz-Scan deckt ALLE owned provinces ab, nicht nur über owned_provinces-Block)
                if (ps.DevClicksAdm + ps.DevClicksDip + ps.DevClicksMil >
                    country.DevClicksAdm + country.DevClicksDip + country.DevClicksMil)
                {
                    country.DevClicksAdm = ps.DevClicksAdm;
                    country.DevClicksDip = ps.DevClicksDip;
                    country.DevClicksMil = ps.DevClicksMil;
                }

                // Manpower aus Provinzen wenn nicht im Länder-Block gespeichert
                if (country.MaxManpower == 0 && ps.MaxManpower > 0)
                    country.MaxManpower = Math.Round(ps.MaxManpower, 2);
                if (country.Manpower == 0 && ps.MaxManpower > 0)
                    country.Manpower = Math.Round(ps.MaxManpower * 0.8, 2);

                // Einkommen aus Provinzdaten wenn kein Ledger vorhanden
                if (country.MonthlyIncome == 0 && ps.ProvinceCount > 0)
                {
                    country.IncomeTax = Math.Round(ps.TaxIncome, 2);
                    country.IncomeProduction = Math.Round(ps.ProdIncome, 2);
                    country.IncomeTrade = Math.Round(ps.TaxIncome * 0.3, 2);
                    country.MonthlyIncome = Math.Round(ps.TaxIncome + ps.ProdIncome + country.IncomeTrade, 2);
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
            bool isHuman = ClausewitzParser.GetString(cData, "human") == "yes";
            int provinces = ClausewitzParser.GetInt(cData, "num_of_cities");
            int rawDev = (int)ClausewitzParser.GetDouble(cData, "raw_development");
            double treasury = ClausewitzParser.GetDouble(cData, "treasury");

            // Land überspringen wenn: keine Provinzen UND keine Entwicklung UND kein Spieler
            // raw_development > 0 zeigt verlässlich an dass ein Land existiert
            // treasury != 0 ist ein weiterer Indikator (auch negative Werte = Schulden)
            bool hasTerritory = provinces > 0 || rawDev > 0 || Math.Abs(treasury) > 0.01;
            if (!hasTerritory && !isHuman) return null;

            var country = new Country
            {
                Tag = tag,
                IsHuman = isHuman,
                ProvinceCount = provinces,
                // raw_development = Gesamtentwicklung (Tax+Prod+MP aller Provinzen)
                TotalDevelopment = rawDev > 0 ? rawDev : (int)ClausewitzParser.GetDouble(cData, "development"),
            };

            // Landesname: aus Save-File lesen, sonst aus Tag-Wörterbuch
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

        /// <summary>
        /// Liest den Ländernamen aus dem history-Block.
        /// EU4 speichert den Namen in history={ ... name="France" ... }
        /// </summary>
        private static string ParseCountryName(Dictionary<string, object> cData, string fallback)
        {
            // Direkt im Country-Block
            string name = ClausewitzParser.GetString(cData, "name");
            if (!string.IsNullOrEmpty(name)) return name;

            // Im history-Block suchen
            var history = ClausewitzParser.GetDict(cData, "history");
            if (history != null)
            {
                name = ClausewitzParser.GetString(history, "name");
                if (!string.IsNullOrEmpty(name)) return name;

                // In history-Einträgen nach add_accepted_culture oder changed_country_name
                foreach (var kv in history)
                {
                    if (kv.Value is Dictionary<string, object> entry)
                    {
                        name = ClausewitzParser.GetString(entry, "changed_country_name");
                        if (!string.IsNullOrEmpty(name)) return name;
                    }
                }
            }

            return fallback;
        }

        // ============================================================
        // Wirtschaft
        // ============================================================

        private void ParseEconomy(Dictionary<string, object> cData, Country country)
        {
            country.Treasury = ClausewitzParser.GetDouble(cData, "treasury");

            // Ledger enthält lastmonthincome und lastmonthexpense als direkte Zahlen
            var ledger = ClausewitzParser.GetDict(cData, "ledger");
            if (ledger != null)
            {
                country.MonthlyIncome = ClausewitzParser.GetDouble(ledger, "lastmonthincome");
                country.MonthlyExpenses = ClausewitzParser.GetDouble(ledger, "lastmonthexpense");

                // Aufschlüsselung aus lastmonthincometable / lastmonthexpensetable
                // Diese sind _values-Listen mit Dezimalzahlen in fester Reihenfolge:
                // Income:  0=Steuern, 1=Produktion, 2=Handel, 3=Gold, 4=Tributstaaten, ...
                // Expense: 0=Armee, 1=Marine, 2=Gebäude/Forts, 3=Berater, 4=Zinsen, ...
                var incTable = ClausewitzParser.GetDict(ledger, "lastmonthincometable");
                var expTable = ClausewitzParser.GetDict(ledger, "lastmonthexpensetable");

                var incValues = GetValuesAsList(incTable);
                var expValues = GetValuesAsList(expTable);

                if (incValues.Count > 0)
                {
                    country.IncomeTax = GetDoubleAtIndex(incValues, 0);
                    country.IncomeProduction = GetDoubleAtIndex(incValues, 1);
                    country.IncomeTrade = GetDoubleAtIndex(incValues, 2);
                    country.IncomeGold = GetDoubleAtIndex(incValues, 3);
                    country.IncomeTribute = GetDoubleAtIndex(incValues, 4)
                                             + GetDoubleAtIndex(incValues, 5);
                }
                else if (country.MonthlyIncome > 0)
                {
                    country.IncomeTax = country.MonthlyIncome * 0.35;
                    country.IncomeProduction = country.MonthlyIncome * 0.28;
                    country.IncomeTrade = country.MonthlyIncome * 0.27;
                    country.IncomeGold = country.MonthlyIncome * 0.10;
                }

                if (expValues.Count > 0)
                {
                    country.ExpenseArmy = GetDoubleAtIndex(expValues, 0);
                    country.ExpenseNavy = GetDoubleAtIndex(expValues, 1);
                    country.ExpenseBuildings = GetDoubleAtIndex(expValues, 2);
                    double totalExp = expValues.Sum();
                    country.ExpenseOther = Math.Max(0,
                        totalExp - country.ExpenseArmy - country.ExpenseNavy - country.ExpenseBuildings);
                }
                else if (country.MonthlyExpenses > 0)
                {
                    country.ExpenseArmy = country.MonthlyExpenses * 0.50;
                    country.ExpenseNavy = country.MonthlyExpenses * 0.15;
                    country.ExpenseBuildings = country.MonthlyExpenses * 0.20;
                    country.ExpenseOther = country.MonthlyExpenses * 0.15;
                }
            }
            else
            {
                // Fallback für ältere Saves
                country.MonthlyIncome = ClausewitzParser.GetDouble(cData, "estimated_monthly_income");
                country.MonthlyExpenses = ClausewitzParser.GetDouble(cData, "estimated_monthly_expenses");
            }
        }

    // ============================================================
    // Militär
    // ============================================================

        /// <summary>
        /// EU4 mil_tech → Force Limit Bonus Lookup-Tabelle.
        /// Werte aus EU4 game files (technologies.txt).
        /// Index = mil_tech level (0-32)
        /// </summary>
    private static readonly int[] MilTechForceLimitBonus =
    {
    //  0   1   2   3   4   5   6   7   8   9
        0,  0,  2,  2,  3,  3,  3,  3,  3,  3,
    //  10  11  12  13  14  15  16  17  18  19
        5,  3,  3,  5,  3,  5,  3,  5,  3,  5,
    //  20  21  22  23  24  25  26  27  28  29  30  31  32
        3,  5,  3,  5,  3,  5,  5,  5,  5,  5,  5,  5,  5
    };

    private void ParseMilitary(Dictionary<string, object> cData, Country country)
    {
        // Manpower direkt im Country-Block
        country.Manpower = ClausewitzParser.GetDouble(cData, "manpower");
        country.MaxManpower = ClausewitzParser.GetDouble(cData, "max_manpower");

        // Armeegröße aus army-Blöcken (Regimenter zählen)
        int armySize = 0;
        int navySize = 0;

        if (cData.TryGetValue("army", out var armyObj))
            foreach (var a in NormalizeToList(armyObj).OfType<Dictionary<string, object>>())
                if (a.TryGetValue("regiment", out var regObj))
                    armySize += NormalizeToList(regObj).Count;

        if (cData.TryGetValue("navy", out var navyObj))
            foreach (var n in NormalizeToList(navyObj).OfType<Dictionary<string, object>>())
                if (n.TryGetValue("ship", out var shipObj))
                    navySize += NormalizeToList(shipObj).Count;

        // Force Limit: Berechnung aus mil_tech + Development
        // Quelle: EU4 technologies.txt + EU4 Wiki
        // Formel: base(10) + Σ ForceLimit-Bonus[tech_level] + development/10
        int forceLimit = 0;
        int navalFL = 0;

        var tech = ClausewitzParser.GetDict(cData, "technology");
        if (tech != null)
        {
            int milTech = ClausewitzParser.GetInt(tech, "mil_tech");
            int navalTech = ClausewitzParser.GetInt(tech, "naval_tech");
            int admTech = ClausewitzParser.GetInt(tech, "adm_tech");

            // Kumulierter Force Limit Bonus aus tech-Tabelle
            int techFL = 10; // Basis Force Limit
            for (int t = 1; t <= Math.Min(milTech, MilTechForceLimitBonus.Length - 1); t++)
                techFL += MilTechForceLimitBonus[t];

            // Development-Bonus: 1 FL pro 10 development
            int devFL = Math.Max(0, country.TotalDevelopment / 10);
            forceLimit = techFL + devFL;

            // Naval Force Limit: Näherung
            navalFL = Math.Max(3, (int)(country.TotalDevelopment / 15.0) + navalTech / 2);
        }

        // Fallback wenn kein tech-Block: Schätzung aus Development
        if (forceLimit == 0)
            forceLimit = Math.Max(5, 10 + country.TotalDevelopment / 10);
        if (navalFL == 0)
            navalFL = Math.Max(3, country.TotalDevelopment / 20);

        country.ForceLimit = forceLimit;
        country.NavalForceLimit = navalFL;

        // Army Size: aus army-Blöcken wenn vorhanden, sonst Force Limit × 75%
        // (EU4-Länder füllen typischerweise 60-90% ihres Force Limits)
        country.ArmySize = armySize > 0 ? armySize : (int)(forceLimit * 0.75);
        country.NavySize = navySize;
    }

    // ============================================================
    // Mana
    // ============================================================

    private void ParseMana(Dictionary<string, object> cData, Country country)
    {
        // Aktuelles Mana aus "powers"-Block: { adm=150 dip=200 mil=100 }
        var powers = ClausewitzParser.GetDict(cData, "powers");
        if (powers != null)
        {
            // powers ist ein _values-Array: [adm_value, dip_value, mil_value]
            var vals = GetValuesAsList(powers);
            if (vals.Count >= 3)
            {
                country.AdmPower = (int)vals[0];
                country.DipPower = (int)vals[1];
                country.MilPower = (int)vals[2];
            }
            else
            {
                // Manchmal als Key-Value
                country.AdmPower = ClausewitzParser.GetInt(powers, "adm");
                country.DipPower = ClausewitzParser.GetInt(powers, "dip");
                country.MilPower = ClausewitzParser.GetInt(powers, "mil");
            }
        }

        // Generiertes Mana aus adm_spent_indexed + aktuellem Vorrat schätzen
        // EU4 hat kein direktes "total generated" Feld mehr in 1.35+
        // Wir nehmen adm_spent_indexed Summe + aktuellen Vorrat als Annäherung
        var admSpent = ClausewitzParser.GetDict(cData, "adm_spent_indexed");
        var dipSpent = ClausewitzParser.GetDict(cData, "dip_spent_indexed");
        var milSpent = ClausewitzParser.GetDict(cData, "mil_spent_indexed");

        int totalAdmSpent = ClausewitzParser.SumDictValues(admSpent);
        int totalDipSpent = ClausewitzParser.SumDictValues(dipSpent);
        int totalMilSpent = ClausewitzParser.SumDictValues(milSpent);

        // Generiert = Ausgegeben + aktueller Vorrat (Näherung)
        country.TotalAdmGenerated = totalAdmSpent + country.AdmPower;
        country.TotalDipGenerated = totalDipSpent + country.DipPower;
        country.TotalMilGenerated = totalMilSpent + country.MilPower;

        // ADM: 0=Stabilität, 1=Tech, 2=Ideen, 7=Provinzentwicklung (base_tax dev clicks)
        country.AdmSpentStability = ClausewitzParser.SumIndices(admSpent, 0);
        country.AdmSpentTech = ClausewitzParser.SumIndices(admSpent, 1);
        country.AdmSpentIdeas = ClausewitzParser.SumIndices(admSpent, 2);
        // Index 7 = Mana für Provinz-Entwicklung (nicht als "Other" zählen)
        int admSpentDev = ClausewitzParser.SumIndices(admSpent, 7);
        country.AdmSpentOther = Math.Max(0,
            totalAdmSpent - country.AdmSpentStability - country.AdmSpentTech
            - country.AdmSpentIdeas - admSpentDev);

        // DIP: 1=Tech, 2=Ideen, 3-6=Diplomatie, 7=Provinzentwicklung (base_production)
        country.DipSpentTech = ClausewitzParser.SumIndices(dipSpent, 1);
        country.DipSpentIdeas = ClausewitzParser.SumIndices(dipSpent, 2);
        country.DipSpentDiplomacy = ClausewitzParser.SumIndices(dipSpent, 3, 4, 5, 6);
        int dipSpentDev = ClausewitzParser.SumIndices(dipSpent, 7);
        country.DipSpentOther = Math.Max(0,
            totalDipSpent - country.DipSpentTech - country.DipSpentIdeas
            - country.DipSpentDiplomacy - dipSpentDev);

        // MIL: 0=Tech, 1=Ideen, 4-5=Generale, 7=Provinzentwicklung (base_manpower)
        country.MilSpentTech = ClausewitzParser.SumIndices(milSpent, 0);
        country.MilSpentIdeas = ClausewitzParser.SumIndices(milSpent, 1);
        country.MilSpentLeaders = ClausewitzParser.SumIndices(milSpent, 4, 5);
        int milSpentDev = ClausewitzParser.SumIndices(milSpent, 7);
        country.MilSpentOther = Math.Max(0,
            totalMilSpent - country.MilSpentTech - country.MilSpentIdeas
            - country.MilSpentLeaders - milSpentDev);

        // Entwicklungsclicks: Index 5=ADM-Dev, 5=DIP-Dev, 5=MIL-Dev (Province Development)
        country.DevClicksAdm = ClausewitzParser.SumIndices(admSpent, 5, 6, 7);
        country.DevClicksDip = ClausewitzParser.SumIndices(dipSpent, 5, 6, 7);
        country.DevClicksMil = ClausewitzParser.SumIndices(milSpent, 5, 6, 7);
    }

    // ============================================================
    // Herrscher
    // ============================================================

    private void ParseRuler(Dictionary<string, object> cData, Country country)
    {
        // monarch-Block hat nur id/type in 1.35+ → echter Herrscher in history
        // Suche den neuesten Herrscher-Eintrag in history
        var history = ClausewitzParser.GetDict(cData, "history");
        if (history == null) return;

        // History-Einträge nach Datum sortieren und letzten Monarch-Eintrag finden
        Dictionary<string, object>? latestMonarch = null;

        foreach (var kv in history)
        {
            if (kv.Value is not Dictionary<string, object> entry) continue;

            // Direkter Monarch-Block in einem Datums-Eintrag
            var m = ClausewitzParser.GetDict(entry, "monarch");
            if (m != null && m.ContainsKey("adm"))
            {
                latestMonarch = m;
                // Nicht break — wir wollen den LETZTEN (neuesten) Eintrag
            }

            // Manchmal als "add_queen" oder direkt als Felder
            var q = ClausewitzParser.GetDict(entry, "queen");
            if (q != null && q.ContainsKey("adm") && latestMonarch == null)
                latestMonarch = q;
        }

        if (latestMonarch != null)
        {
            country.RulerAdm = ClausewitzParser.GetInt(latestMonarch, "adm");
            country.RulerDip = ClausewitzParser.GetInt(latestMonarch, "dip");
            country.RulerMil = ClausewitzParser.GetInt(latestMonarch, "mil");
        }
    }

    // ============================================================
    // Entwicklungsclicks aus Provinzdaten
    // ============================================================

    /// <summary>
    /// Berechnet die tatsächliche Anzahl der Entwicklungsclicks pro Mana-Typ.
    ///
    /// EU4 speichert Provinzen als negative Root-Keys (z.B. -135 für Provinz 135).
    /// Die Anfangswerte (1444 Start) sind im history-Block der Provinz als direkte Keys gespeichert:
    ///   history={ base_tax=3  base_production=3  base_manpower=2 }
    ///
    /// Dev Clicks = Aktueller Wert - Anfangswert
    /// (nie negativ, da Plague/Raid die Entwicklung senken kann)
    /// </summary>
    private void ParseDevClicksFromProvinces(
        Dictionary<string, object> root,
        Dictionary<string, object> cData,
        Country country)
    {
        // owned_provinces enthält Provinz-IDs als _values Liste (z.B. "135", "153", ...)
        var ownedBlock = ClausewitzParser.GetDict(cData, "owned_provinces");
        if (ownedBlock == null) return;

        if (!ownedBlock.TryGetValue("_values", out var vObj) || vObj is not List<string> provIds)
            return;

        int admClicks = 0, dipClicks = 0, milClicks = 0;

        foreach (string idStr in provIds)
        {
            // Provinzen sind negative Root-Keys: Provinz 135 → "-135"
            string negKey = "-" + idStr.Trim();
            if (!root.TryGetValue(negKey, out var provObj)) continue;
            if (provObj is not Dictionary<string, object> prov) continue;

            // Aktuelle Entwicklungswerte der Provinz
            int curTax = (int)ClausewitzParser.GetDouble(prov, "base_tax");
            int curProd = (int)ClausewitzParser.GetDouble(prov, "base_production");
            int curMP = (int)ClausewitzParser.GetDouble(prov, "base_manpower");

            // Anfangswerte aus dem History-Block (nicht datiert, direkter Key im history-Dict)
            // EU4 speichert die Startwerte als base_tax=X direkt im history={} Block
            var hist = ClausewitzParser.GetDict(prov, "history");
            int initTax = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_tax", curTax) : curTax;
            int initProd = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_production", curProd) : curProd;
            int initMP = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_manpower", curMP) : curMP;

            // Falls kein original_tax vorhanden: alternativ aus original_tax-Feld
            if (initTax == curTax)
                initTax = (int)ClausewitzParser.GetDouble(prov, "original_tax", curTax);

            // Dev Clicks = Differenz; Math.Max(0) da Plague/Raid dev senken kann
            admClicks += Math.Max(0, curTax - initTax);
            dipClicks += Math.Max(0, curProd - initProd);
            milClicks += Math.Max(0, curMP - initMP);
        }

        country.DevClicksAdm = admClicks;
        country.DevClicksDip = dipClicks;
        country.DevClicksMil = milClicks;
    }

    // ============================================================
    // Provinz-Scan: Aggregiert Daten aller Provinzen pro Besitzer
    // ============================================================

    /// <summary>
    /// Scannt alle Provinzen im Save und aggregiert pro Besitzer-Tag:
    /// Provinzanzahl, Gesamtentwicklung, Armee- und Flotteneinheiten.
    ///
    /// Provinzen sind als negative Root-Keys gespeichert: -135, -153 etc.
    /// Jede Provinz hat: owner="FRA", base_tax=X, base_production=Y, base_manpower=Z
    ///
    /// Dies gibt verlässliche Daten für ALLE Länder — auch für die ~500 Nationen
    /// deren Länder-Block keine vollständigen Laufzeitdaten enthält.
    /// </summary>
    private Dictionary<string, ProvinceStats> ScanProvinces(Dictionary<string, object> root)
    {
        var stats = new Dictionary<string, ProvinceStats>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in root)
        {
            // Provinzen = negative Integer-Keys: "-135", "-1772" etc.
            if (!kv.Key.StartsWith("-")) continue;
            if (!int.TryParse(kv.Key, out _)) continue;
            if (kv.Value is not Dictionary<string, object> prov) continue;

            // Nur Provinzen mit einem Besitzer
            string owner = ClausewitzParser.GetString(prov, "owner");
            if (string.IsNullOrEmpty(owner)) continue;

            int tax = (int)ClausewitzParser.GetDouble(prov, "base_tax");
            int prod = (int)ClausewitzParser.GetDouble(prov, "base_production");
            int mp = (int)ClausewitzParser.GetDouble(prov, "base_manpower");

            // Initiale Werte aus History für DevClicks
            var hist = ClausewitzParser.GetDict(prov, "history");
            int initTax = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_tax", tax) : tax;
            int initProd = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_production", prod) : prod;
            int initMp = hist != null ? (int)ClausewitzParser.GetDouble(hist, "base_manpower", mp) : mp;
            if (initTax == tax) initTax = (int)ClausewitzParser.GetDouble(prov, "original_tax", tax);

            if (!stats.ContainsKey(owner))
                stats[owner] = new ProvinceStats();

            var s = stats[owner];
            s.ProvinceCount++;
            s.TotalDevelopment += tax + prod + mp;
            s.DevClicksAdm += Math.Max(0, tax - initTax);
            s.DevClicksDip += Math.Max(0, prod - initProd);
            s.DevClicksMil += Math.Max(0, mp - initMp);
            stats[owner] = s;
        }

        return stats;
    }

    /// <summary>Aggregierte Provinzdaten pro Nation.</summary>
    private class ProvinceStats
    {
        public int ProvinceCount { get; set; }
        public int TotalDevelopment { get; set; }
        public int DevClicksAdm { get; set; }
        public int DevClicksDip { get; set; }
        public int DevClicksMil { get; set; }
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
            IsActive = isActive,
            Name = ClausewitzParser.GetString(warData, "name"),
            WarScore = ClausewitzParser.GetDouble(warData, "superiority"),
            Outcome = isActive ? "ongoing" : "unknown"
        };

        // Start/Enddatum aus history-Block: die Keys sind die Daten (z.B. "1444.11.11")
        var history = ClausewitzParser.GetDict(warData, "history");
        if (history != null)
        {
            // Alle Datum-Keys sammeln und sortieren
            var dateKeys = history.Keys
                .Where(k => k.Length >= 8 && k.Contains('.') && char.IsDigit(k[0]))
                .OrderBy(k => k)
                .ToList();

            if (dateKeys.Count > 0)
                war.StartDate = dateKeys.First();
            if (!isActive && dateKeys.Count > 1)
                war.EndDate = dateKeys.Last();
        }

        // Kriegsausgang aus action-Feld
        if (!isActive)
        {
            string action = ClausewitzParser.GetString(warData, "action");
            war.Outcome = action switch
            {
                "call_to_peace" => "draw",
                _ when war.WarScore > 0 => "attacker_win",
                _ when war.WarScore < 0 => "defender_win",
                _ => "unknown"
            };
        }

        // Kriegsparteien aus attackers/defenders
        var attackers = new List<string>();
        var defenders = new List<string>();

        // original_attacker / original_defender als direkte Tags
        string origAtt = ClausewitzParser.GetString(warData, "original_attacker");
        string origDef = ClausewitzParser.GetString(warData, "original_defender");
        if (!string.IsNullOrEmpty(origAtt)) attackers.Add(origAtt);
        if (!string.IsNullOrEmpty(origDef)) defenders.Add(origDef);

        // persistent_attackers / persistent_defenders (Listen von Tags)
        if (warData.TryGetValue("persistent_attackers", out var paObj))
        {
            var pa = paObj as Dictionary<string, object>;
            if (pa != null)
            {
                var vals = GetValuesAsList(pa);
                attackers.AddRange(vals.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v)));
            }
        }
        if (warData.TryGetValue("persistent_defenders", out var pdObj))
        {
            var pd = pdObj as Dictionary<string, object>;
            if (pd != null)
            {
                var vals = GetValuesAsList(pd);
                defenders.AddRange(vals.Select(v => v.ToString()).Where(v => !string.IsNullOrEmpty(v)));
            }
        }

        // participants-Block als Fallback
        if (attackers.Count <= 1 && warData.TryGetValue("participants", out var partObj))
        {
            foreach (var p in NormalizeToList(partObj).OfType<Dictionary<string, object>>())
            {
                string tag = ClausewitzParser.GetString(p, "tag");
                string side = ClausewitzParser.GetString(p, "value");
                if (string.IsNullOrEmpty(tag)) continue;
                if (side == "1") attackers.Add(tag);
                else defenders.Add(tag);
            }
        }

        war.AttackerTags = string.Join(", ", attackers.Distinct().Where(t => !string.IsNullOrEmpty(t)));
        war.DefenderTags = string.Join(", ", defenders.Distinct().Where(t => !string.IsNullOrEmpty(t)));

        // Verluste aus history
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

    /// <summary>
    /// Liest _values aus einem Block als Liste von doubles.
    /// EU4 speichert Tabellen als bare-value Listen in Blöcken.
    /// </summary>
    private static List<double> GetValuesAsList(Dictionary<string, object>? dict)
    {
        if (dict == null) return new List<double>();
        if (!dict.TryGetValue("_values", out var raw)) return new List<double>();

        if (raw is List<string> ls)
        {
            return ls.Select(s =>
            {
                if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
                return 0.0;
            }).ToList();
        }
        return new List<double>();
    }

    private static double GetDoubleAtIndex(List<double> list, int index)
        => index < list.Count ? list[index] : 0.0;
}
