using System.ComponentModel.DataAnnotations;

namespace EU4SaveAnalyzer.Models;

/// <summary>
/// Repräsentiert eine hochgeladene EU4 Save-Datei mit Metadaten.
/// </summary>
public class SaveGame
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>Datum des Spielstands im EU4-Format (z.B. 1500.3.15)</summary>
    [MaxLength(20)]
    public string GameDate { get; set; } = string.Empty;

    /// <summary>Nation-Tag des menschlichen Spielers</summary>
    [MaxLength(10)]
    public string PlayerTag { get; set; } = string.Empty;

    /// <summary>Zeitpunkt des Uploads</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public ICollection<Country> Countries { get; set; } = new List<Country>();
    public ICollection<War> Wars { get; set; } = new List<War>();
}
