using System.ComponentModel.DataAnnotations;

namespace EU4SaveAnalyzer.Models;

/// <summary>
/// Repräsentiert eine Nation aus dem EU4 Save-File mit allen relevanten Statistiken.
/// </summary>
public class Country
{
    public int Id { get; set; }

    /// <summary>Fremdschlüssel zum übergeordneten SaveGame</summary>
    public int SaveGameId { get; set; }
    public SaveGame SaveGame { get; set; } = null!;

    // --- Identifikation ---
    /// <summary>Dreistelliger Ländercode (z.B. FRA, ENG, AUS)</summary>
    [MaxLength(10)]
    public string Tag { get; set; } = string.Empty;

    /// <summary>Anzeigename der Nation</summary>
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Spielername falls menschlicher Spieler, sonst null/leer = KI</summary>
    [MaxLength(100)]
    public string? PlayerName { get; set; }

    /// <summary>True wenn diese Nation von einem Menschen gespielt wird</summary>
    public bool IsHuman { get; set; }

    // --- Wirtschaft ---
    /// <summary>Aktueller Schatzinhalt in Dukaten</summary>
    public double Treasury { get; set; }

    /// <summary>Monatliches Einkommen in Dukaten</summary>
    public double MonthlyIncome { get; set; }

    /// <summary>Monatliche Ausgaben in Dukaten</summary>
    public double MonthlyExpenses { get; set; }

    /// <summary>Einnahmen aus Steuern</summary>
    public double IncomeTax { get; set; }

    /// <summary>Einnahmen aus Produktion</summary>
    public double IncomeProduction { get; set; }

    /// <summary>Einnahmen aus Handel</summary>
    public double IncomeTrade { get; set; }

    /// <summary>Einnahmen aus Goldminen</summary>
    public double IncomeGold { get; set; }

    /// <summary>Einnahmen aus Tributstaaten</summary>
    public double IncomeTribute { get; set; }

    /// <summary>Gesamtausgaben Armeeunterhalt</summary>
    public double ExpenseArmy { get; set; }

    /// <summary>Gesamtausgaben Marine</summary>
    public double ExpenseNavy { get; set; }

    /// <summary>Ausgaben für Gebäude und Entwicklung</summary>
    public double ExpenseBuildings { get; set; }

    /// <summary>Weitere Ausgaben (Söldner, Subsidien etc.)</summary>
    public double ExpenseOther { get; set; }

    // --- Militär ---
    /// <summary>Anzahl der Infanterie-, Kavallerie- und Artillerieregimenter</summary>
    public int ArmySize { get; set; }

    /// <summary>Maximales Force Limit</summary>
    public int ForceLimit { get; set; }

    /// <summary>Verfügbare Manpower (in Tausend)</summary>
    public double Manpower { get; set; }

    /// <summary>Maximale Manpower (in Tausend)</summary>
    public double MaxManpower { get; set; }

    /// <summary>Anzahl der Kriegsschiffe</summary>
    public int NavySize { get; set; }

    /// <summary>Navales Force Limit</summary>
    public int NavalForceLimit { get; set; }

    // --- Mana ---
    /// <summary>Aktuelles ADM-Power</summary>
    public int AdmPower { get; set; }

    /// <summary>Aktuelles DIP-Power</summary>
    public int DipPower { get; set; }

    /// <summary>Aktuelles MIL-Power</summary>
    public int MilPower { get; set; }

    /// <summary>Gesamtes generiertes ADM-Mana (Summe aller Quellen)</summary>
    public int TotalAdmGenerated { get; set; }

    /// <summary>Gesamtes generiertes DIP-Mana</summary>
    public int TotalDipGenerated { get; set; }

    /// <summary>Gesamtes generiertes MIL-Mana</summary>
    public int TotalMilGenerated { get; set; }

    /// <summary>ADM-Mana ausgegeben für Technologie</summary>
    public int AdmSpentTech { get; set; }

    /// <summary>ADM-Mana ausgegeben für Ideen</summary>
    public int AdmSpentIdeas { get; set; }

    /// <summary>ADM-Mana ausgegeben für Stabilität</summary>
    public int AdmSpentStability { get; set; }

    /// <summary>ADM-Mana ausgegeben für sonstiges (Kultur, Kernland etc.)</summary>
    public int AdmSpentOther { get; set; }

    /// <summary>DIP-Mana für Technologie</summary>
    public int DipSpentTech { get; set; }

    /// <summary>DIP-Mana für Ideen</summary>
    public int DipSpentIdeas { get; set; }

    /// <summary>DIP-Mana für Annexionen und Diplomatie</summary>
    public int DipSpentDiplomacy { get; set; }

    /// <summary>DIP-Mana für sonstiges</summary>
    public int DipSpentOther { get; set; }

    /// <summary>MIL-Mana für Technologie</summary>
    public int MilSpentTech { get; set; }

    /// <summary>MIL-Mana für Ideen</summary>
    public int MilSpentIdeas { get; set; }

    /// <summary>MIL-Mana für Generale und Admirale</summary>
    public int MilSpentLeaders { get; set; }

    /// <summary>MIL-Mana für sonstiges</summary>
    public int MilSpentOther { get; set; }

    // --- Herrscherstatistiken ---
    /// <summary>ADM-Wert des aktuellen Herrschers (0-6)</summary>
    public int RulerAdm { get; set; }

    /// <summary>DIP-Wert des aktuellen Herrschers (0-6)</summary>
    public int RulerDip { get; set; }

    /// <summary>MIL-Wert des aktuellen Herrschers (0-6)</summary>
    public int RulerMil { get; set; }

    /// <summary>Durchschnittswert des Herrschers (ADM+DIP+MIL)/3</summary>
    public double RulerAvg => (RulerAdm + RulerDip + RulerMil) / 3.0;

    // --- Territorium ---
    /// <summary>Anzahl der Provinzen</summary>
    public int ProvinceCount { get; set; }

    /// <summary>Gesamtentwicklung (Dev) aller Provinzen</summary>
    public int TotalDevelopment { get; set; }
}
