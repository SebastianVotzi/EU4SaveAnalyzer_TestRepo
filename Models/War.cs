using System.ComponentModel.DataAnnotations;

namespace EU4SaveAnalyzer.Models;

/// <summary>
/// Repräsentiert einen Krieg (aktiv oder beendet) aus dem EU4 Save-File.
/// </summary>
public class War
{
    public int Id { get; set; }

    /// <summary>Fremdschlüssel zum übergeordneten SaveGame</summary>
    public int SaveGameId { get; set; }
    public SaveGame SaveGame { get; set; } = null!;

    // --- Metadaten ---
    [MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Startdatum des Krieges im EU4-Format</summary>
    [MaxLength(20)]
    public string StartDate { get; set; } = string.Empty;

    /// <summary>Enddatum des Krieges (leer wenn noch aktiv)</summary>
    [MaxLength(20)]
    public string EndDate { get; set; } = string.Empty;

    /// <summary>True wenn der Krieg noch läuft</summary>
    public bool IsActive { get; set; }

    // --- Parteien ---
    /// <summary>Komma-getrennte Liste der Angreifer-Tags</summary>
    [MaxLength(500)]
    public string AttackerTags { get; set; } = string.Empty;

    /// <summary>Komma-getrennte Liste der Verteidiger-Tags</summary>
    [MaxLength(500)]
    public string DefenderTags { get; set; } = string.Empty;

    // --- Ergebnis ---
    /// <summary>Kriegsausgang: "attacker_win", "defender_win", "draw", "ongoing"</summary>
    [MaxLength(30)]
    public string Outcome { get; set; } = "ongoing";

    // --- Verluste ---
    /// <summary>Verluste der Angreiferseite</summary>
    public int AttackerLosses { get; set; }

    /// <summary>Verluste der Verteidigerseite</summary>
    public int DefenderLosses { get; set; }

    // --- Warscore ---
    public double WarScore { get; set; }
}
