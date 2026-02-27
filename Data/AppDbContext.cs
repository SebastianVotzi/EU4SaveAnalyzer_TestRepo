using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Models;

namespace EU4SaveAnalyzer.Data;

/// <summary>
/// Entity Framework Core Datenbankkontext für SQLite.
/// Verwaltet alle Datenbanktabellen des EU4 Save-Analyzers.
///
/// Registrierung in Program.cs:
///   builder.Services.AddDbContext&lt;AppDbContext&gt;(options =>
///       options.UseSqlite("Data Source=eu4saves.db"));
///
/// EF Core erstellt die SQLite-Datei beim ersten Aufruf von Database.EnsureCreated()
/// automatisch im Arbeitsverzeichnis der Anwendung.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Konstruktor der von ASP.NET Core Dependency Injection aufgerufen wird.
    /// Die DbContextOptions enthalten Verbindungsstring und Provider (SQLite).
    /// </summary>
    /// <param name="options">Konfigurationsoptionen (Verbindungsstring, Provider).</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Tabelle "SaveGames": Ein Eintrag pro hochgeladener .eu4-Datei.
    /// Über SaveGame.Countries und SaveGame.Wars erreichbar (Navigation Properties).
    /// </summary>
    public DbSet<SaveGame> SaveGames { get; set; }

    /// <summary>
    /// Tabelle "Countries": Alle Nationen aus allen hochgeladenen Saves.
    /// Jede Nation gehört über SaveGameId genau einem SaveGame.
    /// </summary>
    public DbSet<Country> Countries { get; set; }

    /// <summary>
    /// Tabelle "Wars": Alle Kriege (aktiv + vergangen) aus allen Saves.
    /// Jeder Krieg gehört über SaveGameId genau einem SaveGame.
    /// </summary>
    public DbSet<War> Wars { get; set; }

    /// <summary>
    /// Konfiguriert Beziehungen, Indizes und Löschverhalten zwischen den Entitäten.
    /// Wird von EF Core beim Start (Database.EnsureCreated) aufgerufen.
    /// </summary>
    /// <param name="modelBuilder">EF Core Builder zum Konfigurieren des DB-Schemas.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SaveGame → Countries: cascade delete
        modelBuilder.Entity<Country>()
            .HasOne(c => c.SaveGame)
            .WithMany(s => s.Countries)
            .HasForeignKey(c => c.SaveGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // SaveGame → Wars: cascade delete
        modelBuilder.Entity<War>()
            .HasOne(w => w.SaveGame)
            .WithMany(s => s.Wars)
            .HasForeignKey(w => w.SaveGameId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index für häufige Queries nach SaveGameId
        modelBuilder.Entity<Country>()
            .HasIndex(c => c.SaveGameId);

        modelBuilder.Entity<Country>()
            .HasIndex(c => new { c.SaveGameId, c.Tag })
            .IsUnique();

        modelBuilder.Entity<War>()
            .HasIndex(w => w.SaveGameId);
    }
}
