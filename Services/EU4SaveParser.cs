using System.IO.Compression;
using System.Text;
using EU4SaveAnalyzer.Models;

namespace EU4SaveAnalyzer.Services;

/// <summary>
/// Verarbeitet eine EU4 Save-Datei (.eu4) und extrahiert alle relevanten Daten.
/// EU4-Saves sind entweder:
///  - Direkte Textdateien (Clausewitz-Format)
///  - ZIP-Archive mit einer "gamestate"-Datei darin (komprimierte Saves)
/// Ironman-Saves (binär) werden nicht unterstützt.
/// </summary>
public class EU4SaveParser
{
    // Länder-Tags die ignoriert werden (Rebels, Pirates, etc.)
    private static readonly HashSet<string> _ignoredTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "REB", "PIR", "NAT", "OOO", "---"
    };

    /// <summary>
    /// Liest eine EU4 Save-Datei (ZIP oder Text) und gibt den geparsten Root-Node zurück.
    /// </summary>
    public async Task<(SaveGame save, List<Country> countries, List<War> wars)>
        ParseAsync(Stream fileStream, string fileName)
    {
        string rawText = await ReadSaveTextAsync(fileStream);

        // Parser starten
        var parser = new ClausewitzParser(rawText);
        var root = parser.Parse();

        var save = new SaveGame
        {
            FileName = fileName,
            GameDate = ClausewitzParser.GetString(root, "date"),
            PlayerTag = ClausewitzParser.GetString(root, "player").Trim('"'),
            UploadedAt = DateTime.UtcNow
        };

        var countries = ParseCountries(root, save.PlayerTag);
        var wars = ParseWars(root);

        return (save, countries, wars);
    }

    /// <summary>
    /// Liest den Text aus einem EU4-Save. Falls es ein ZIP ist, wird "gamestate" extrahiert.
    /// Falls Ironman-Binärformat erkannt wird, wird eine Exception geworfen.
    /// </summary>
    private async Task<string> ReadSaveTextAsync(Stream stream)
    {
        // In MemoryStream lesen um mehrfaches Lesen zu ermöglichen
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        ms.Position = 0;

        // Prüfen ob ZIP (EU4-Saves beginnen mit PK)
        byte[] header = new byte[4];
        ms.Read(header, 0, 4);
        ms.Position = 0;

        if (header[0] == 0x50 && header[1] == 0x4B) // PK = ZIP
        {
            return await ExtractFromZip(ms);
        }

        // Prüfen ob Binär-Ironman-Format (EU4bin)
        if (header[0] == 0x45 && header[1] == 0x55) // "EU"
        {
            throw new InvalidDataException(
                "Ironman/Binär-Saves werden nicht unterstützt. " +
                "Bitte verwende einen normalen (nicht-Ironman) Spielstand.");
        }

        // Normaler Text-Save
        ms.Position = 0;
        using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Extrahiert den "gamestate" Text aus dem ZIP-Archiv eines EU4-Saves.</summary>
    private async Task<string> ExtractFromZip(MemoryStream ms)
    {
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);

        // EU4 speichert den Hauptinhalt in "gamestate"
        var entry = zip.GetEntry("gamestate")
                    ?? zip.Entries.FirstOrDefault(e =>
                        e.Name.EndsWith(".eu4", StringComparison.OrdinalIgnoreCase))
                    ?? zip.Entries.First();

        await using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream, Encoding.Latin1);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Extrahiert alle Länder-Daten aus dem geparsten Root-Dictionary.
    /// Überspringt KI-Nationen ohne Provinzen und spezielle System-Tags.
    /// </summary>
    private List<Country> ParseCountries(Dictionary<string, object> root, string playerTag)
    {
        var result = new List<Country>();

        var countriesNode = ClausewitzParser.GetDict(root, "countries");
        if (countriesNode == null) return result;

        foreach (var kv in countriesNode)
        {
            string tag = kv.Key;

            // System-Tags und Rebellen überspringen
            if (_ignoredTags.Contains(tag)) continue;
            if (tag.StartsWith("D") && tag.Length == 3 &&
                char.IsDigit(tag[1]) && char.IsDigit(tag[2])) continue; // Dynamic tags (D01 etc)

            if (kv.Value is not Dictionary<string, object> cData) continue;

            // Nationen ohne Provinzen (aufgelöst) überspringen
            int provinces = CountProvinces(root, tag);
            if (provinces == 0 && ClausewitzParser.GetString(cData, "human") != "yes") continue;

            var country = new Country
            {
                Tag = tag,
                Name = ClausewitzParser.GetString(cData, "name", tag),
                IsHuman = ClausewitzParser.GetString(cData, "human") == "yes",
                ProvinceCount = provinces
            };

            // Spielername setzen (nur beim menschlichen Spieler)
            if (country.IsHuman)
                country.PlayerName = ClausewitzParser.GetString(cData, "human", "");

            // Falls der Tag dem Spieler-Tag entspricht und Name leer ist
            if (tag.Equals(playerTag, StringComparison.OrdinalIgnoreCase) && country.IsHuman)
                country.PlayerName = playerTag; // Mindest-Fallback

            // Wirtschaft
            country.Treasury = ClausewitzParser.GetDouble(cData, "treasury");
            country.MonthlyIncome = ClausewitzParser.GetDouble(cData, "estimated_monthly_income");
            country.MonthlyExpenses = ClausewitzParser.GetDouble(cData, "estimated_monthly_expenses");

            // Einnahmen-Details aus "income" oder "ledger"
            ParseIncome(cData, country);

            // Ausgaben-Details
            ParseExpenses(cData, country);

            // Militär
            country.ForceLimit = ClausewitzParser.GetInt(cData, "land_force_limit");
            country.NavalForceLimit = ClausewitzParser.GetInt(cData, "naval_force_limit");
            country.Manpower = ClausewitzParser.GetDouble(cData, "manpower");
            country.MaxManpower = ClausewitzParser.GetDouble(cData, "max_manpower");
            country.ArmySize = ClausewitzParser.GetInt(cData, "num_of_land_units");
            country.NavySize = ClausewitzParser.GetInt(cData, "num_of_ships");

            // Mana-Werte
            country.AdmPower = ClausewitzParser.GetInt(cData, "adm_power");
            country.DipPower = ClausewitzParser.GetInt(cData, "dip_power");
            country.MilPower = ClausewitzParser.GetInt(cData, "mil_power");

            // Generiertes Mana
            country.TotalAdmGenerated = ClausewitzParser.GetInt(cData, "adm_power_generated");
            country.TotalDipGenerated = ClausewitzParser.GetInt(cData, "dip_power_generated");
            country.TotalMilGenerated = ClausewitzParser.GetInt(cData, "mil_power_generated");

            // Mana-Ausgaben (indexed arrays)
            ParseManaSpent(cData, country);

            // Herrscher
            ParseRuler(cData, country);

            // Entwicklung
            country.TotalDevelopment = ClausewitzParser.GetInt(cData, "raw_development");

            result.Add(country);
        }

        return result.OrderByDescending(c => c.IsHuman)
                     .ThenByDescending(c => c.TotalDevelopment)
                     .ToList();
    }

    /// <summary>Zählt die Provinzen einer Nation aus dem Map-Data.</summary>
    private int CountProvinces(Dictionary<string, object> root, string tag)
    {
        // Schnelle Methode: "num_of_cities" direkt im Country-Node
        var countriesNode = ClausewitzParser.GetDict(root, "countries");
        if (countriesNode == null) return 0;
        if (!countriesNode.TryGetValue(tag, out var cObj)) return 0;
        if (cObj is not Dictionary<string, object> cData) return 0;

        int cities = ClausewitzParser.GetInt(cData, "num_of_cities");
        return cities;
    }

    /// <summary>Parst Einnahmen-Details aus dem Country-Dictionary.</summary>
    private void ParseIncome(Dictionary<string, object> cData, Country country)
    {
        // EU4 speichert income_statistics als indexed array
        // Alternativ direkt als einzelne Felder
        country.IncomeTax = ClausewitzParser.GetDouble(cData, "tax_income");
        country.IncomeProduction = ClausewitzParser.GetDouble(cData, "production_income");
        country.IncomeTrade = ClausewitzParser.GetDouble(cData, "trade_income");
        country.IncomeGold = ClausewitzParser.GetDouble(cData, "gold_income");
        country.IncomeTribute = ClausewitzParser.GetDouble(cData, "tribute_income");

        // Falls estimated_monthly_income vorhanden aber Einzelwerte nicht, schätze
        if (country.MonthlyIncome > 0 &&
            country.IncomeTax == 0 && country.IncomeProduction == 0 &&
            country.IncomeTrade == 0)
        {
            country.IncomeTax = country.MonthlyIncome * 0.35;
            country.IncomeProduction = country.MonthlyIncome * 0.30;
            country.IncomeTrade = country.MonthlyIncome * 0.25;
            country.IncomeGold = country.MonthlyIncome * 0.10;
        }
    }

    /// <summary>Parst Ausgaben-Details aus dem Country-Dictionary.</summary>
    private void ParseExpenses(Dictionary<string, object> cData, Country country)
    {
        country.ExpenseArmy = ClausewitzParser.GetDouble(cData, "army_expense");
        country.ExpenseNavy = ClausewitzParser.GetDouble(cData, "navy_expense");
        country.ExpenseBuildings = ClausewitzParser.GetDouble(cData, "buildings_expense");
        country.ExpenseOther = ClausewitzParser.GetDouble(cData, "advisor_expense")
                             + ClausewitzParser.GetDouble(cData, "interest_expense");

        // Fallback falls keine Einzelwerte
        if (country.MonthlyExpenses > 0 &&
            country.ExpenseArmy == 0 && country.ExpenseNavy == 0)
        {
            country.ExpenseArmy = country.MonthlyExpenses * 0.50;
            country.ExpenseNavy = country.MonthlyExpenses * 0.15;
            country.ExpenseBuildings = country.MonthlyExpenses * 0.20;
            country.ExpenseOther = country.MonthlyExpenses * 0.15;
        }
    }

    /// <summary>
    /// Parst Mana-Ausgaben aus den indexed-Arrays.
    /// EU4 ADM-Index: 0=Stabilität, 1=Technologie, 2=Ideen, 3=Kultur, 8=Kernland, etc.
    /// EU4 DIP-Index: 0=Stabilität, 1=Technologie, 2=Ideen, 3=Relationen, 5=Annexion, etc.
    /// EU4 MIL-Index: 0=Technologie, 1=Ideen, 4=Generale, 5=Belagerung, etc.
    /// </summary>
    private void ParseManaSpent(Dictionary<string, object> cData, Country country)
    {
        var admSpent = ClausewitzParser.GetDict(cData, "adm_spent_indexed");
        var dipSpent = ClausewitzParser.GetDict(cData, "dip_spent_indexed");
        var milSpent = ClausewitzParser.GetDict(cData, "mil_spent_indexed");

        // ADM: Index 0=Stabilität, 1=Tech, 2=Ideen, Rest=Sonstiges
        country.AdmSpentStability = ClausewitzParser.SumIndices(admSpent, 0);
        country.AdmSpentTech = ClausewitzParser.SumIndices(admSpent, 1);
        country.AdmSpentIdeas = ClausewitzParser.SumIndices(admSpent, 2);
        int totalAdm = ClausewitzParser.SumDictValues(admSpent);
        country.AdmSpentOther = totalAdm - country.AdmSpentStability
                               - country.AdmSpentTech - country.AdmSpentIdeas;

        // DIP: Index 0=Stabilität, 1=Tech, 2=Ideen, 5=Annexion/Diplomatie
        country.DipSpentTech = ClausewitzParser.SumIndices(dipSpent, 1);
        country.DipSpentIdeas = ClausewitzParser.SumIndices(dipSpent, 2);
        country.DipSpentDiplomacy = ClausewitzParser.SumIndices(dipSpent, 3, 4, 5, 6, 7, 8);
        int totalDip = ClausewitzParser.SumDictValues(dipSpent);
        country.DipSpentOther = totalDip - country.DipSpentTech
                               - country.DipSpentIdeas - country.DipSpentDiplomacy;

        // MIL: Index 0=Tech, 1=Ideen, 4=Generale
        country.MilSpentTech = ClausewitzParser.SumIndices(milSpent, 0);
        country.MilSpentIdeas = ClausewitzParser.SumIndices(milSpent, 1);
        country.MilSpentLeaders = ClausewitzParser.SumIndices(milSpent, 4, 5);
        int totalMil = ClausewitzParser.SumDictValues(milSpent);
        country.MilSpentOther = totalMil - country.MilSpentTech
                               - country.MilSpentIdeas - country.MilSpentLeaders;

        // Negative Werte auf 0 setzen (bei unbekannten Indizes)
        country.AdmSpentOther = Math.Max(0, country.AdmSpentOther);
        country.DipSpentOther = Math.Max(0, country.DipSpentOther);
        country.MilSpentOther = Math.Max(0, country.MilSpentOther);
    }

    /// <summary>Parst den aktuellen Herrscher des Landes.</summary>
    private void ParseRuler(Dictionary<string, object> cData, Country country)
    {
        // Monarch-Node
        var monarch = ClausewitzParser.GetDict(cData, "monarch");
        if (monarch == null)
        {
            // Fallback: history → monarch
            return;
        }

        country.RulerAdm = ClausewitzParser.GetInt(monarch, "adm");
        country.RulerDip = ClausewitzParser.GetInt(monarch, "dip");
        country.RulerMil = ClausewitzParser.GetInt(monarch, "mil");
    }

    /// <summary>Parst alle aktiven und vergangenen Kriege.</summary>
    private List<War> ParseWars(Dictionary<string, object> root)
    {
        var result = new List<War>();

        // Aktive Kriege
        if (root.TryGetValue("active_war", out var activeWarObj))
        {
            var wars = NormalizeToList(activeWarObj);
            foreach (var w in wars)
            {
                if (w is Dictionary<string, object> warData)
                    result.Add(ParseSingleWar(warData, isActive: true));
            }
        }

        // Vergangene Kriege
        if (root.TryGetValue("previous_war", out var prevWarObj))
        {
            var wars = NormalizeToList(prevWarObj);
            foreach (var w in wars)
            {
                if (w is Dictionary<string, object> warData)
                    result.Add(ParseSingleWar(warData, isActive: false));
            }
        }

        return result;
    }

    /// <summary>Normalisiert einen Wert in eine Liste (für Felder die einmal oder mehrfach vorkommen).</summary>
    private List<object> NormalizeToList(object obj)
    {
        if (obj is List<object> list) return list;
        if (obj is Dictionary<string, object> dict) return new List<object> { dict };
        return new List<object>();
    }

    /// <summary>Parst einen einzelnen Kriegs-Node.</summary>
    private War ParseSingleWar(Dictionary<string, object> warData, bool isActive)
    {
        var war = new War
        {
            IsActive = isActive,
            Name = ClausewitzParser.GetString(warData, "name"),
            StartDate = ClausewitzParser.GetString(warData, "start_date"),
            WarScore = ClausewitzParser.GetDouble(warData, "battle_score")
        };

        if (!isActive)
        {
            war.EndDate = ClausewitzParser.GetString(warData, "end_date");
            string outcome = ClausewitzParser.GetString(warData, "outcome");
            war.Outcome = outcome switch
            {
                "1" => "attacker_win",
                "2" => "defender_win",
                "3" => "draw",
                _ => "unknown"
            };
        }
        else
        {
            war.Outcome = "ongoing";
        }

        // Parteien
        var attackers = new List<string>();
        var defenders = new List<string>();

        if (warData.TryGetValue("attackers", out var attObj))
        {
            foreach (var entry in NormalizeToList(attObj))
                if (entry is Dictionary<string, object> d)
                    attackers.Add(ClausewitzParser.GetString(d, "country"));
        }

        if (warData.TryGetValue("defenders", out var defObj))
        {
            foreach (var entry in NormalizeToList(defObj))
                if (entry is Dictionary<string, object> d)
                    defenders.Add(ClausewitzParser.GetString(d, "country"));
        }

        war.AttackerTags = string.Join(", ", attackers.Where(t => !string.IsNullOrEmpty(t)));
        war.DefenderTags = string.Join(", ", defenders.Where(t => !string.IsNullOrEmpty(t)));

        // Verluste aus History
        ParseWarLosses(warData, war);

        return war;
    }

    /// <summary>Parst die Verluste beider Kriegsparteien aus dem History-Node.</summary>
    private void ParseWarLosses(Dictionary<string, object> warData, War war)
    {
        var history = ClausewitzParser.GetDict(warData, "history");
        if (history == null) return;

        int attackerTotal = 0;
        int defenderTotal = 0;

        foreach (var kv in history)
        {
            if (kv.Value is not Dictionary<string, object> evt) continue;
            // Schlachten: "battle" node
            if (evt.TryGetValue("battle", out var battleObj) &&
                battleObj is Dictionary<string, object> battle)
            {
                var attacker = ClausewitzParser.GetDict(battle, "attacker");
                var defender = ClausewitzParser.GetDict(battle, "defender");
                attackerTotal += ClausewitzParser.GetInt(attacker ?? new(), "losses");
                defenderTotal += ClausewitzParser.GetInt(defender ?? new(), "losses");
            }
        }

        war.AttackerLosses = attackerTotal;
        war.DefenderLosses = defenderTotal;
    }
}
