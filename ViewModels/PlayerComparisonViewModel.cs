namespace EU4SaveAnalyzer.ViewModels;

/// <summary>
/// ViewModel für die Spieler-Vergleichsseite.
/// Enthält ausschließlich Nationen die von einem menschlichen Spieler gesteuert werden (IsHuman == true).
/// </summary>
public class PlayerComparisonViewModel
{
    /// <summary>ID des SaveGames aus dem die Spieler stammen.</summary>
    public int SaveGameId { get; set; }

    /// <summary>Spieldatum im EU4-Format (z.B. "1550.6.14"), wird in der Navbar angezeigt.</summary>
    public string GameDate { get; set; } = string.Empty;

    /// <summary>
    /// Liste aller menschlichen Spieler im Save.
    /// Leer wenn kein Multiplayer-Save hochgeladen wurde.
    /// </summary>
    public List<PlayerData> Players { get; set; } = new();

    /// <summary>
    /// Tags der Spieler die für den direkten Vergleich ausgewählt wurden.
    /// Kommt aus dem Query-Parameter "selectedTags" (z.B. "FRA,ENG").
    /// </summary>
    public List<string> SelectedTags { get; set; } = new();

    /// <summary>
    /// Gefilterte Spieler für den Side-by-Side Vergleich.
    /// Enthält nur die Spieler deren Tag in SelectedTags vorkommt.
    /// Wenn keine Tags ausgewählt, werden alle Spieler zurückgegeben.
    /// </summary>
    public List<PlayerData> SelectedPlayers =>
        SelectedTags.Count == 0
            ? Players
            : Players.Where(p => SelectedTags.Contains(p.Tag)).ToList();
}

/// <summary>
/// Enthält alle vergleichbaren Statistiken eines einzelnen menschlichen Spielers.
/// Wird aus dem Country-Model befüllt und für Charts und Tabellen verwendet.
/// </summary>
public class PlayerData
{
    // ---- Identifikation ----

    /// <summary>Dreistelliger EU4-Ländercode (z.B. "FRA", "ENG", "OTT").</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>Vollständiger Landesname (z.B. "France", "England").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Steam-/Spielername des menschlichen Spielers. Nie null bei PlayerData.</summary>
    public string PlayerName { get; set; } = string.Empty;

    // ---- Wirtschaft ----

    /// <summary>Aktueller Schatzinhalt in Dukaten (kann negativ = Schulden sein).</summary>
    public double Treasury { get; set; }

    /// <summary>Geschätzte monatliche Einnahmen in Dukaten.</summary>
    public double MonthlyIncome { get; set; }

    /// <summary>Geschätzte monatliche Ausgaben in Dukaten.</summary>
    public double MonthlyExpenses { get; set; }

    /// <summary>Monatlicher Gewinn/Verlust (Einnahmen minus Ausgaben).</summary>
    public double Profit => MonthlyIncome - MonthlyExpenses;
    /// <summary>Formatierter Profit-String mit Vorzeichen.</summary>
    public string ProfitDisplay => (Profit >= 0 ? "+" : "") + Profit.ToString("N1");

    // ---- Militär ----

    /// <summary>Anzahl der aktuellen Landeinheiten (Infanterie + Kavallerie + Artillerie).</summary>
    public int ArmySize { get; set; }

    /// <summary>Maximales Land-Force-Limit (abhängig von Entwicklung + Ideen).</summary>
    public int ForceLimit { get; set; }

    /// <summary>Verfügbare Manpower in Tausend.</summary>
    public double Manpower { get; set; }

    /// <summary>Anzahl der Kriegsschiffe.</summary>
    public int NavySize { get; set; }

    // ---- Mana ----

    /// <summary>Aktuell gespeichertes ADM-Power (max. 999).</summary>
    public int AdmPower { get; set; }

    /// <summary>Aktuell gespeichertes DIP-Power (max. 999).</summary>
    public int DipPower { get; set; }

    /// <summary>Aktuell gespeichertes MIL-Power (max. 999).</summary>
    public int MilPower { get; set; }

    /// <summary>Gesamtes je generiertes ADM-Mana (Spielfortschritts-Indikator).</summary>
    public int TotalAdmGenerated { get; set; }

    /// <summary>Gesamtes je generiertes DIP-Mana.</summary>
    public int TotalDipGenerated { get; set; }

    /// <summary>Gesamtes je generiertes MIL-Mana.</summary>
    public int TotalMilGenerated { get; set; }

    // ---- Herrscher ----

    /// <summary>ADM-Stat des aktuellen Herrschers (0–6).</summary>
    public int RulerAdm { get; set; }

    /// <summary>DIP-Stat des aktuellen Herrschers (0–6).</summary>
    public int RulerDip { get; set; }

    /// <summary>MIL-Stat des aktuellen Herrschers (0–6).</summary>
    public int RulerMil { get; set; }

    /// <summary>Durchschnitt aller drei Herrscherwerte, gerundet auf eine Nachkommastelle.</summary>
    public double RulerAvg => Math.Round((RulerAdm + RulerDip + RulerMil) / 3.0, 1);

    // ---- Territorium ----

    /// <summary>Anzahl der kontrollierten Provinzen.</summary>
    public int ProvinceCount { get; set; }

    /// <summary>Gesamte Entwicklung (Steuern + Produktion + Mannschaft aller Provinzen).</summary>
    public int TotalDevelopment { get; set; }

    // ---- Ausgaben-Aufschlüsselung (für Tortendiagramm) ----

    /// <summary>Monatliche Ausgaben für Armeeunterhalt.</summary>
    public double ExpenseArmy { get; set; }

    /// <summary>Monatliche Ausgaben für Marineunterhalt.</summary>
    public double ExpenseNavy { get; set; }

    /// <summary>Monatliche Ausgaben für Gebäude/Entwicklung.</summary>
    public double ExpenseBuildings { get; set; }

    /// <summary>Alle anderen Ausgaben (Berater, Subsidien, Zinsen).</summary>
    public double ExpenseOther { get; set; }

    // ---- Mana-Ausgaben (für gestapeltes Balkendiagramm) ----

    /// <summary>ADM-Mana ausgegeben für Technologie-Upgrades.</summary>
    public int AdmSpentTech { get; set; }

    /// <summary>ADM-Mana ausgegeben für nationale Ideen.</summary>
    public int AdmSpentIdeas { get; set; }

    /// <summary>ADM-Mana ausgegeben für Stabilität.</summary>
    public int AdmSpentStability { get; set; }

    /// <summary>ADM-Mana für alle anderen Zwecke (Kultur konvertieren, Kernland etc.).</summary>
    public int AdmSpentOther { get; set; }

    /// <summary>DIP-Mana ausgegeben für Technologie.</summary>
    public int DipSpentTech { get; set; }

    /// <summary>DIP-Mana ausgegeben für nationale Ideen.</summary>
    public int DipSpentIdeas { get; set; }

    /// <summary>DIP-Mana für Diplomatie (Annexion, Relationen, Vasallen etc.).</summary>
    public int DipSpentDiplomacy { get; set; }

    /// <summary>DIP-Mana für alle anderen Zwecke.</summary>
    public int DipSpentOther { get; set; }

    /// <summary>MIL-Mana ausgegeben für Technologie.</summary>
    public int MilSpentTech { get; set; }

    /// <summary>MIL-Mana ausgegeben für nationale Ideen.</summary>
    public int MilSpentIdeas { get; set; }

    /// <summary>MIL-Mana für das Rekrutieren von Generalen und Admiralen.</summary>
    public int MilSpentLeaders { get; set; }

    /// <summary>MIL-Mana für alle anderen Zwecke (z.B. Belagerungen beschleunigen).</summary>
    public int MilSpentOther { get; set; }

    // ---- Computed (Berechnete Eigenschaften für die View) ----

    /// <summary>
    /// Summe aller ausgegebenen ADM-Mana-Punkte über den gesamten Spielverlauf.
    /// Dient als Effizienz-Indikator: Je mehr ausgegeben, desto aktiver der Spieler.
    /// </summary>
    public int TotalAdmSpent => AdmSpentTech + AdmSpentIdeas + AdmSpentStability + AdmSpentOther;

    /// <summary>Summe aller ausgegebenen DIP-Mana-Punkte.</summary>
    public int TotalDipSpent => DipSpentTech + DipSpentIdeas + DipSpentDiplomacy + DipSpentOther;

    /// <summary>Summe aller ausgegebenen MIL-Mana-Punkte.</summary>
    public int TotalMilSpent => MilSpentTech + MilSpentIdeas + MilSpentLeaders + MilSpentOther;

    /// <summary>Gesamte generierte Mana-Punkte über alle drei Typen.</summary>
    public int TotalManaGenerated => TotalAdmGenerated + TotalDipGenerated + TotalMilGenerated;

    /// <summary>
    /// Prozentualer Anteil der Armeekosten an den Gesamtausgaben.
    /// Hilfreich um militärisch aggressive Spieler zu identifizieren.
    /// </summary>
    public double ArmyExpensePercent =>
        MonthlyExpenses > 0 ? Math.Round(ExpenseArmy / MonthlyExpenses * 100, 1) : 0;

    /// <summary>ADM-Entwicklungsclicks (Mana in Provinzentwicklung investiert).</summary>
    public int DevClicksAdm { get; set; }

    /// <summary>DIP-Entwicklungsclicks.</summary>
    public int DevClicksDip { get; set; }

    /// <summary>MIL-Entwicklungsclicks.</summary>
    public int DevClicksMil { get; set; }

    /// <summary>Gesamte Entwicklungsclicks aller Mana-Typen.</summary>
    public int TotalDevClicks => DevClicksAdm + DevClicksDip + DevClicksMil;
}
