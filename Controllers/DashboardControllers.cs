using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Data;
using EU4SaveAnalyzer.ViewModels;

namespace EU4SaveAnalyzer.Controllers;

// ============================================================
//  MILITARY CONTROLLER
// ============================================================

/// <summary>
/// Dashboard für Militärstatistiken: Armeegröße, Force Limit, Manpower, Marine.
/// </summary>
public class MilitaryController : Controller
{
    private readonly AppDbContext _db;
    public MilitaryController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int saveId, string? search)
    {
        var save = await _db.SaveGames.FindAsync(saveId);
        if (save == null) return RedirectToAction("Index", "Home");

        var query = _db.Countries
            .Where(c => c.SaveGameId == saveId && c.ArmySize > 0);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Tag.Contains(search));

        var countries = await query
            .OrderByDescending(c => c.ArmySize)
            .Take(25)
            .ToListAsync();

        var vm = new MilitaryViewModel
        {
            SaveGameId = saveId,
            GameDate = save.GameDate,
            SearchTerm = search,
            Countries = countries.Select(c => new MilitaryCountryData
            {
                Tag = c.Tag,
                Name = c.Name,
                PlayerName = c.IsHuman ? c.PlayerName : null,
                IsHuman = c.IsHuman,
                ArmySize = c.ArmySize,
                ForceLimit = c.ForceLimit,
                Manpower = c.Manpower,
                MaxManpower = c.MaxManpower,
                NavySize = c.NavySize,
                NavalForceLimit = c.NavalForceLimit
            }).ToList()
        };

        return View(vm);
    }

    /// <summary>AJAX: Militärvergleichs-Chart-Daten.</summary>
    [HttpGet]
    public async Task<IActionResult> ChartData(int saveId)
    {
        var countries = await _db.Countries
            .Where(c => c.SaveGameId == saveId && c.ArmySize > 0)
            .OrderByDescending(c => c.ArmySize)
            .Take(15)
            .ToListAsync();

        return Json(new
        {
            labels = countries.Select(c => c.Name).ToArray(),
            armySize = countries.Select(c => c.ArmySize).ToArray(),
            forceLimit = countries.Select(c => c.ForceLimit).ToArray(),
            manpower = countries.Select(c => Math.Round(c.Manpower, 1)).ToArray(),
            navySize = countries.Select(c => c.NavySize).ToArray()
        });
    }
}

// ============================================================
//  SPENDING CONTROLLER
// ============================================================

/// <summary>
/// Dashboard für Ausgaben-Analyse: Aufschlüsselung der monatlichen Ausgaben nach Kategorien.
/// </summary>
public class SpendingController : Controller
{
    private readonly AppDbContext _db;
    public SpendingController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int saveId, string? search)
    {
        var save = await _db.SaveGames.FindAsync(saveId);
        if (save == null) return RedirectToAction("Index", "Home");

        var query = _db.Countries
            .Where(c => c.SaveGameId == saveId && c.MonthlyExpenses > 0);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Tag.Contains(search));

        var countries = await query
            .OrderByDescending(c => c.MonthlyExpenses)
            .Take(20)
            .ToListAsync();

        var vm = new SpendingViewModel
        {
            SaveGameId = saveId,
            GameDate = save.GameDate,
            SearchTerm = search,
            Countries = countries.Select(c => new SpendingCountryData
            {
                Tag = c.Tag,
                Name = c.Name,
                PlayerName = c.IsHuman ? c.PlayerName : null,
                IsHuman = c.IsHuman,
                ExpenseArmy = c.ExpenseArmy,
                ExpenseNavy = c.ExpenseNavy,
                ExpenseBuildings = c.ExpenseBuildings,
                ExpenseOther = c.ExpenseOther
            }).ToList()
        };

        return View(vm);
    }

    /// <summary>AJAX: Gibt Ausgaben-Aufschlüsselung für ein einzelnes Land zurück.</summary>
    [HttpGet]
    public async Task<IActionResult> CountryBreakdown(int saveId, string tag)
    {
        var country = await _db.Countries
            .FirstOrDefaultAsync(c => c.SaveGameId == saveId && c.Tag == tag);

        if (country == null) return NotFound();

        return Json(new
        {
            name = country.Name,
            army = Math.Round(country.ExpenseArmy, 2),
            navy = Math.Round(country.ExpenseNavy, 2),
            buildings = Math.Round(country.ExpenseBuildings, 2),
            other = Math.Round(country.ExpenseOther, 2)
        });
    }
}

// ============================================================
//  MANA CONTROLLER
// ============================================================

/// <summary>
/// Dashboard für Mana-Nutzung: ADM/DIP/MIL Generierung und Ausgaben-Kategorien.
/// </summary>
public class ManaController : Controller
{
    private readonly AppDbContext _db;
    public ManaController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int saveId, string? search)
    {
        var save = await _db.SaveGames.FindAsync(saveId);
        if (save == null) return RedirectToAction("Index", "Home");

        var query = _db.Countries
            .Where(c => c.SaveGameId == saveId &&
                       (c.TotalAdmGenerated > 0 || c.TotalDipGenerated > 0 || c.TotalMilGenerated > 0));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Tag.Contains(search));

        var countries = await query
            .OrderByDescending(c => c.TotalAdmGenerated + c.TotalDipGenerated + c.TotalMilGenerated)
            .Take(20)
            .ToListAsync();

        var vm = new ManaViewModel
        {
            SaveGameId = saveId,
            GameDate = save.GameDate,
            SearchTerm = search,
            Countries = countries.Select(c => new ManaCountryData
            {
                Tag = c.Tag,
                Name = c.Name,
                PlayerName = c.IsHuman ? c.PlayerName : null,
                IsHuman = c.IsHuman,
                TotalAdmGenerated = c.TotalAdmGenerated,
                TotalDipGenerated = c.TotalDipGenerated,
                TotalMilGenerated = c.TotalMilGenerated,
                AdmPower = c.AdmPower,
                DipPower = c.DipPower,
                MilPower = c.MilPower,
                AdmSpentTech = c.AdmSpentTech,
                AdmSpentIdeas = c.AdmSpentIdeas,
                AdmSpentStability = c.AdmSpentStability,
                AdmSpentOther = c.AdmSpentOther,
                DipSpentTech = c.DipSpentTech,
                DipSpentIdeas = c.DipSpentIdeas,
                DipSpentDiplomacy = c.DipSpentDiplomacy,
                DipSpentOther = c.DipSpentOther,
                MilSpentTech = c.MilSpentTech,
                MilSpentIdeas = c.MilSpentIdeas,
                MilSpentLeaders = c.MilSpentLeaders,
                MilSpentOther = c.MilSpentOther
            }).ToList()
        };

        return View(vm);
    }

    /// <summary>AJAX: Mana-Chart-Daten (generiert vs. ausgegeben pro Nation).</summary>
    [HttpGet]
    public async Task<IActionResult> ChartData(int saveId, string manaType = "adm")
    {
        var countries = await _db.Countries
            .Where(c => c.SaveGameId == saveId && c.TotalAdmGenerated > 0)
            .OrderByDescending(c => c.TotalAdmGenerated + c.TotalDipGenerated + c.TotalMilGenerated)
            .Take(15)
            .ToListAsync();

        object data = manaType switch
        {
            "adm" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                tech = countries.Select(c => c.AdmSpentTech).ToArray(),
                ideas = countries.Select(c => c.AdmSpentIdeas).ToArray(),
                stability = countries.Select(c => c.AdmSpentStability).ToArray(),
                other = countries.Select(c => c.AdmSpentOther).ToArray()
            },
            "dip" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                tech = countries.Select(c => c.DipSpentTech).ToArray(),
                ideas = countries.Select(c => c.DipSpentIdeas).ToArray(),
                diplomacy = countries.Select(c => c.DipSpentDiplomacy).ToArray(),
                other = countries.Select(c => c.DipSpentOther).ToArray()
            },
            "mil" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                tech = countries.Select(c => c.MilSpentTech).ToArray(),
                ideas = countries.Select(c => c.MilSpentIdeas).ToArray(),
                leaders = countries.Select(c => c.MilSpentLeaders).ToArray(),
                other = countries.Select(c => c.MilSpentOther).ToArray()
            },
            _ => new { }
        };

        return Json(data);
    }
}

// ============================================================
//  WARS CONTROLLER
// ============================================================

/// <summary>
/// Dashboard für Kriegs-Übersicht: aktive und vergangene Kriege mit Verlustzahlen.
/// </summary>
public class WarsController : Controller
{
    private readonly AppDbContext _db;
    public WarsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int saveId, string? search)
    {
        var save = await _db.SaveGames.FindAsync(saveId);
        if (save == null) return RedirectToAction("Index", "Home");

        var query = _db.Wars.Where(w => w.SaveGameId == saveId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(w =>
                w.Name.Contains(search) ||
                w.AttackerTags.Contains(search) ||
                w.DefenderTags.Contains(search));

        var wars = await query
            .OrderByDescending(w => w.IsActive)
            .ThenByDescending(w => w.StartDate)
            .ToListAsync();

        static WarData Map(EU4SaveAnalyzer.Models.War w) => new()
        {
            Id = w.Id,
            Name = w.Name,
            StartDate = w.StartDate,
            EndDate = w.EndDate,
            IsActive = w.IsActive,
            AttackerTags = w.AttackerTags,
            DefenderTags = w.DefenderTags,
            Outcome = w.Outcome,
            AttackerLosses = w.AttackerLosses,
            DefenderLosses = w.DefenderLosses,
            WarScore = w.WarScore
        };

        var vm = new WarsViewModel
        {
            SaveGameId = saveId,
            GameDate = save.GameDate,
            SearchTerm = search,
            ActiveWars = wars.Where(w => w.IsActive).Select(Map).ToList(),
            PreviousWars = wars.Where(w => !w.IsActive).Select(Map).ToList()
        };

        return View(vm);
    }
}

// ============================================================
//  RANKING CONTROLLER
// ============================================================

/// <summary>
/// Nationen-Ranking-Tabelle mit sortierbaren Spalten für alle wichtigen Statistiken.
/// </summary>
public class RankingController : Controller
{
    private readonly AppDbContext _db;
    public RankingController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int saveId, string sortBy = "army", string? search= null)
    {
        var save = await _db.SaveGames.FindAsync(saveId);
        if (save == null) return RedirectToAction("Index", "Home");

        var query = _db.Countries
            .Where(c => c.SaveGameId == saveId && c.ProvinceCount > 0);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Tag.Contains(search));

        // Sortierung nach gewählter Spalte
        query = sortBy switch
        {
            "army"       => query.OrderByDescending(c => c.ArmySize),
            "forcelimit" => query.OrderByDescending(c => c.ForceLimit),
            "manpower"   => query.OrderByDescending(c => c.Manpower),
            "economy"    => query.OrderByDescending(c => c.MonthlyIncome),
            "provinces"  => query.OrderByDescending(c => c.ProvinceCount),
            "ruler"      => query.OrderByDescending(c => c.RulerAdm + c.RulerDip + c.RulerMil),
            "mana"       => query.OrderByDescending(c =>
                               c.TotalAdmGenerated + c.TotalDipGenerated + c.TotalMilGenerated),
            "dev"        => query.OrderByDescending(c => c.TotalDevelopment),
            _            => query.OrderByDescending(c => c.ArmySize)
        };

        var countries = await query.Take(50).ToListAsync();

        var entries = countries.Select((c, i) => new RankingEntry
        {
            Rank = i + 1,
            Tag = c.Tag,
            Name = c.Name,
            // Spielername nur bei menschlichen Spielern anzeigen, sonst leer
            PlayerName = c.IsHuman ? (c.PlayerName ?? c.Tag) : null,
            IsHuman = c.IsHuman,
            ArmySize = c.ArmySize,
            ForceLimit = c.ForceLimit,
            Manpower = c.Manpower,
            MonthlyIncome = c.MonthlyIncome,
            ProvinceCount = c.ProvinceCount,
            RulerAdm = c.RulerAdm,
            RulerDip = c.RulerDip,
            RulerMil = c.RulerMil,
            RulerAvg = c.RulerAvg,
            TotalAdmGenerated = c.TotalAdmGenerated,
            TotalDipGenerated = c.TotalDipGenerated,
            TotalMilGenerated = c.TotalMilGenerated,
            TotalDevelopment = c.TotalDevelopment
        }).ToList();

        var vm = new RankingViewModel
        {
            SaveGameId = saveId,
            GameDate = save.GameDate,
            SortBy = sortBy,
            SearchTerm = search,
            Rankings = entries
        };

        return View(vm);
    }
}
