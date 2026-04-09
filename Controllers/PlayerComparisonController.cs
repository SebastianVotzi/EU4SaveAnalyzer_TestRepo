using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Data;
using EU4SaveAnalyzer.ViewModels;

namespace EU4SaveAnalyzer.Controllers;

/// <summary>
/// Controller für den Spieler-Vergleich (Multiplayer-Analyse).
///
/// Dieser Controller filtert ausschließlich menschliche Spieler (IsHuman == true)
/// aus der Datenbank und stellt deren Statistiken für einen direkten Vergleich bereit.
///
/// Routen:
///   GET  /PlayerComparison?saveId={id}                    → Alle Spieler anzeigen
///   GET  /PlayerComparison?saveId={id}&selectedTags=FRA,ENG → Nur gewählte Spieler vergleichen
///   GET  /PlayerComparison/ChartData?saveId={id}&type={type} → AJAX-Endpunkt für Charts
/// </summary>
public class PlayerComparisonController : Controller
{
    /// <summary>
    /// Datenbankkontext, wird per Dependency Injection bereitgestellt.
    /// Scoped = eine Instanz pro HTTP-Request.
    /// </summary>
    private readonly AppDbContext _db;

    /// <summary>
    /// Konstruktor: Empfängt den DbContext über Dependency Injection (DI).
    /// In Program.cs wird AppDbContext mit AddDbContext registriert,
    /// ASP.NET Core injiziert es automatisch hier.
    /// </summary>
    /// <param name="db">Der EF Core Datenbankkontext für SQLite-Zugriff.</param>
    public PlayerComparisonController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// GET: Hauptansicht des Spieler-Vergleichs.
    ///
    /// Lädt alle menschlichen Spieler eines Saves und rendert die Vergleichsseite.
    /// Wenn "selectedTags" übergeben wird, werden nur diese Spieler im direkten
    /// Vergleich (Side-by-Side) angezeigt — alle bleiben aber als Checkboxen sichtbar.
    ///
    /// Ablauf:
    ///   1. SaveGame aus DB laden → Fehler wenn nicht gefunden
    ///   2. Countries filtern: nur IsHuman == true
    ///   3. Daten in PlayerData-ViewModels mappen
    ///   4. SelectedTags aus Query-String parsen
    ///   5. View rendern
    /// </summary>
    /// <param name="saveId">Primärschlüssel des SaveGame-Eintrags in der DB.</param>
    /// <param name="selectedTags">
    ///     Komma-getrennte Liste von Länder-Tags die verglichen werden sollen.
    ///     Beispiel: "FRA,ENG,OTT". Wenn leer → alle Spieler werden angezeigt.
    /// </param>
    /// <returns>
    ///     View mit PlayerComparisonViewModel, oder Redirect zu Home wenn SaveGame fehlt.
    /// </returns>
    public async Task<IActionResult> Index(int saveId, string? selectedTags)
    {
        // Schritt 1: SaveGame aus der Datenbank holen
        var save = await _db.SaveGames.FindAsync(saveId);

        // Wenn das SaveGame nicht existiert (z.B. gelöscht), zurück zur Startseite leiten
        if (save == null)
            return RedirectToAction("Index", "Home");

        // Schritt 2: Nur menschliche Spieler laden
        // IsHuman wird beim Parsen gesetzt wenn "human = yes" im Save-File steht
        var humanCountries = await _db.Countries
            .Where(c => c.SaveGameId == saveId && c.IsHuman)
            .OrderBy(c => c.Name)  // Alphabetische Reihenfolge für die Checkliste
            .ToListAsync();

        // Schritt 3: Country-Model → PlayerData ViewModel mappen
        // Alle relevanten Felder werden einzeln zugewiesen damit die View
        // keine direkten DB-Model-Abhängigkeiten hat (klare Schichttrennung)
        var players = humanCountries.Select(c => new PlayerData
        {
            // Identifikation
            Tag        = c.Tag,
            Name       = c.Name,
            PlayerName = c.PlayerName ?? c.Tag, // Fallback: Tag wenn kein Name vorhanden

            // Wirtschaft
            Treasury       = c.Treasury,
            MonthlyIncome  = c.MonthlyIncome,
            MonthlyExpenses = c.MonthlyExpenses,

            // Militär
            ArmySize   = c.ArmySize,
            ForceLimit = c.ForceLimit,
            Manpower   = c.Manpower,
            NavySize   = c.NavySize,

            // Mana-Vorrat
            AdmPower = c.AdmPower,
            DipPower = c.DipPower,
            MilPower = c.MilPower,

            // Generiertes Mana (Gesamt seit Spielbeginn)
            TotalAdmGenerated = c.TotalAdmGenerated,
            TotalDipGenerated = c.TotalDipGenerated,
            TotalMilGenerated = c.TotalMilGenerated,

            // Herrscher-Stats
            RulerAdm = c.RulerAdm,
            RulerDip = c.RulerDip,
            RulerMil = c.RulerMil,

            // Territorium
            ProvinceCount    = c.ProvinceCount,
            TotalDevelopment = c.TotalDevelopment,

            // Ausgaben-Kategorien
            ExpenseArmy      = c.ExpenseArmy,
            ExpenseNavy      = c.ExpenseNavy,
            ExpenseBuildings = c.ExpenseBuildings,
            ExpenseOther     = c.ExpenseOther,

            // Mana-Ausgaben ADM
            AdmSpentTech      = c.AdmSpentTech,
            AdmSpentIdeas     = c.AdmSpentIdeas,
            AdmSpentStability = c.AdmSpentStability,
            AdmSpentOther     = c.AdmSpentOther,

            // Mana-Ausgaben DIP
            DipSpentTech      = c.DipSpentTech,
            DipSpentIdeas     = c.DipSpentIdeas,
            DipSpentDiplomacy = c.DipSpentDiplomacy,
            DipSpentOther     = c.DipSpentOther,

            // Mana-Ausgaben MIL
            MilSpentTech    = c.MilSpentTech,
            MilSpentIdeas   = c.MilSpentIdeas,
            MilSpentLeaders = c.MilSpentLeaders,
            MilSpentOther   = c.MilSpentOther,

            // Entwicklungsclicks
            DevClicksAdm = c.DevClicksAdm,
            DevClicksDip = c.DevClicksDip,
            DevClicksMil = c.DevClicksMil,
        }).ToList();

        // Schritt 4: Ausgewählte Tags aus dem Query-String parsen
        // "FRA,ENG,OTT" → ["FRA", "ENG", "OTT"]
        var parsedTags = string.IsNullOrWhiteSpace(selectedTags)
            ? new List<string>()
            : selectedTags
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToUpperInvariant())
                .ToList();

        // Schritt 5: ViewModel zusammenbauen und View aufrufen
        var vm = new PlayerComparisonViewModel
        {
            SaveGameId    = saveId,
            GameDate      = save.GameDate,
            Players       = players,
            SelectedTags  = parsedTags
        };

        // ViewBag-Werte werden vom _Layout für die Navbar benötigt
        ViewBag.SaveId   = saveId;
        ViewBag.GameDate = save.GameDate;

        return View(vm);
    }

    /// <summary>
    /// GET: AJAX-Endpunkt für Chart.js-Diagrammdaten.
    ///
    /// Wird von der View per Fetch API aufgerufen (kein Seitenreload).
    /// Gibt JSON-Daten für verschiedene Chart-Typen zurück.
    ///
    /// Unterstützte Chart-Typen ("type"-Parameter):
    ///   - "economy"  → Einkommen, Ausgaben, Treasury pro Spieler
    ///   - "military" → Armee, Force Limit, Manpower pro Spieler
    ///   - "mana"     → ADM/DIP/MIL generiert pro Spieler
    ///   - "spending" → Ausgaben-Aufschlüsselung pro Spieler (gestapelt)
    ///   - "ruler"    → Herrscherwerte ADM/DIP/MIL pro Spieler
    ///
    /// Beispielaufruf aus JavaScript:
    ///   fetch('/PlayerComparison/ChartData?saveId=1&type=economy&tags=FRA,ENG')
    /// </summary>
    /// <param name="saveId">ID des SaveGames.</param>
    /// <param name="type">Chart-Typ (economy, military, mana, spending, ruler).</param>
    /// <param name="tags">
    ///     Komma-getrennte Tags die in den Chart aufgenommen werden.
    ///     Wenn leer → alle menschlichen Spieler.
    /// </param>
    /// <returns>JSON-Objekt mit "labels"-Array und typ-spezifischen Daten-Arrays.</returns>
    [HttpGet]
    public async Task<IActionResult> ChartData(int saveId, string type = "economy", string? tags = null)
    {
        // Basis-Query: nur menschliche Spieler dieses Saves
        var query = _db.Countries
            .Where(c => c.SaveGameId == saveId && c.IsHuman);

        // Wenn Tags übergeben: nur diese Spieler laden
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var tagList = tags.Split(',').Select(t => t.Trim().ToUpperInvariant()).ToList();
            query = query.Where(c => tagList.Contains(c.Tag));
        }

        var countries = await query.OrderBy(c => c.Name).ToListAsync();

        // Je nach Chart-Typ unterschiedliche JSON-Struktur zurückgeben
        // Alle Zahlen werden auf 1-2 Nachkommastellen gerundet für saubere Darstellung
        object data = type switch
        {
            // Wirtschaftsvergleich: 3 Datensätze pro Spieler
            "economy" => new
            {
                labels   = countries.Select(c => c.Name).ToArray(),
                income   = countries.Select(c => Math.Round(c.MonthlyIncome,  1)).ToArray(),
                expenses = countries.Select(c => Math.Round(c.MonthlyExpenses, 1)).ToArray(),
                treasury = countries.Select(c => Math.Round(c.Treasury,        0)).ToArray()
            },

            // Militärvergleich: Armee (Bar) + Force Limit (Line)
            "military" => new
            {
                labels     = countries.Select(c => c.Name).ToArray(),
                army       = countries.Select(c => c.ArmySize).ToArray(),
                forceLimit = countries.Select(c => c.ForceLimit).ToArray(),
                manpower   = countries.Select(c => Math.Round(c.Manpower, 1)).ToArray(),
                navy       = countries.Select(c => c.NavySize).ToArray()
            },

            // Mana-Generierung: 3 gestapelte Balken (ADM / DIP / MIL)
            "mana" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                adm    = countries.Select(c => c.TotalAdmGenerated).ToArray(),
                dip    = countries.Select(c => c.TotalDipGenerated).ToArray(),
                mil    = countries.Select(c => c.TotalMilGenerated).ToArray()
            },

            // Ausgaben-Aufschlüsselung: 4 gestapelte Balken
            "spending" => new
            {
                labels    = countries.Select(c => c.Name).ToArray(),
                army      = countries.Select(c => Math.Round(c.ExpenseArmy,      1)).ToArray(),
                navy      = countries.Select(c => Math.Round(c.ExpenseNavy,      1)).ToArray(),
                buildings = countries.Select(c => Math.Round(c.ExpenseBuildings, 1)).ToArray(),
                other     = countries.Select(c => Math.Round(c.ExpenseOther,     1)).ToArray()
            },

            // Herrscherwerte: Grouped Bar (ADM, DIP, MIL nebeneinander)
            "ruler" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                adm    = countries.Select(c => c.RulerAdm).ToArray(),
                dip    = countries.Select(c => c.RulerDip).ToArray(),
                mil    = countries.Select(c => c.RulerMil).ToArray()
            },

            // Unbekannter Typ → leeres Objekt, kein Fehler
            _ => new { }
        };

        return Json(data);
    }
}
