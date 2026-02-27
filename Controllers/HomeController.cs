using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Data;
using EU4SaveAnalyzer.Models;
using EU4SaveAnalyzer.Services;
using EU4SaveAnalyzer.ViewModels;

namespace EU4SaveAnalyzer.Controllers;

/// <summary>
/// Controller für die Startseite: Listet vorhandene Save-Dateien und ermöglicht den Upload.
/// </summary>
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly EU4SaveParser _parser;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext db, EU4SaveParser parser, ILogger<HomeController> logger)
    {
        _db = db;
        _parser = parser;
        _logger = logger;
    }

    /// <summary>Startseite: Zeigt Liste aller hochgeladenen Save-Dateien.</summary>
    public async Task<IActionResult> Index()
    {
        var saves = await _db.SaveGames
            .Include(s => s.Countries)
            .OrderByDescending(s => s.UploadedAt)
            .ToListAsync();

        var vm = new HomeViewModel
        {
            SaveGames = saves.Select(s => new SaveGameListItem
            {
                Id = s.Id,
                FileName = s.FileName,
                GameDate = s.GameDate,
                PlayerTag = s.PlayerTag,
                UploadedAt = s.UploadedAt,
                CountryCount = s.Countries.Count
            }).ToList()
        };

        // Erfolgsmeldung aus TempData übernehmen
        if (TempData["Success"] is string success)
            vm.SuccessMessage = success;
        if (TempData["Error"] is string error)
            vm.ErrorMessage = error;

        return View(vm);
    }

    /// <summary>
    /// POST: Verarbeitet den Upload einer EU4 Save-Datei.
    /// Parst die Datei und speichert alle Daten in der SQLite-Datenbank.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? saveFile)
    {
        // Validierung: Datei vorhanden?
        if (saveFile == null || saveFile.Length == 0)
        {
            TempData["Error"] = "Bitte wähle eine EU4 Save-Datei aus.";
            return RedirectToAction(nameof(Index));
        }

        // Validierung: Dateigröße (max 500MB)
        if (saveFile.Length > 500 * 1024 * 1024)
        {
            TempData["Error"] = "Die Datei ist zu groß (max. 500 MB).";
            return RedirectToAction(nameof(Index));
        }

        // Validierung: Dateiendung
        var ext = Path.GetExtension(saveFile.FileName).ToLowerInvariant();
        if (ext != ".eu4" && ext != ".zip")
        {
            TempData["Error"] = "Nur .eu4 Dateien werden unterstützt.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _logger.LogInformation("Verarbeite Save-Datei: {FileName}", saveFile.FileName);

            await using var stream = saveFile.OpenReadStream();
            var (save, countries, wars) = await _parser.ParseAsync(stream, saveFile.FileName);

            // In Datenbank speichern
            _db.SaveGames.Add(save);
            await _db.SaveChangesAsync();

            // Länder und Kriege mit SaveGameId verknüpfen
            foreach (var c in countries) c.SaveGameId = save.Id;
            foreach (var w in wars) w.SaveGameId = save.Id;

            _db.Countries.AddRange(countries);
            _db.Wars.AddRange(wars);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Save erfolgreich gespeichert: {Id}, {Count} Länder, {Wars} Kriege",
                save.Id, countries.Count, wars.Count);

            TempData["Success"] = $"Save '{saveFile.FileName}' erfolgreich geladen! " +
                                  $"{countries.Count} Nationen, {wars.Count} Kriege gefunden.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning("Ungültiges Save-Format: {Message}", ex.Message);
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Parsen der Save-Datei");
            TempData["Error"] = $"Fehler beim Lesen der Datei: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>Löscht einen Save-Eintrag aus der Datenbank (inkl. aller verknüpften Daten).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var save = await _db.SaveGames.FindAsync(id);
        if (save == null)
        {
            TempData["Error"] = "Save nicht gefunden.";
            return RedirectToAction(nameof(Index));
        }

        _db.SaveGames.Remove(save);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Save '{save.FileName}' wurde gelöscht.";
        return RedirectToAction(nameof(Index));
    }
}
