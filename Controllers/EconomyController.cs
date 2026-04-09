using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Data;
using EU4SaveAnalyzer.ViewModels;

namespace EU4SaveAnalyzer.Controllers;

/// <summary>
/// Controller für das Wirtschafts-Dashboard.
/// Zeigt Einkommen, Ausgaben und Treasury-Daten aller Nationen.
/// </summary>
public class EconomyController : Controller
{
    private readonly AppDbContext _db;

    public EconomyController(AppDbContext db) => _db = db;

    /// <summary>Hauptansicht: Wirtschaftsübersicht mit Balken- und Tortendiagramm.</summary>
    public async Task<IActionResult> Index(int saveId, string? search, string? filter, int page = 1)
    {
        var save = await _db.SaveGames.FindAsync(saveId);
        if (save == null) return RedirectToAction("Index", "Home");

        const int pageSize = 50;

        var query = _db.Countries
            .Where(c => c.SaveGameId == saveId && c.TotalDevelopment > 0);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.Tag.Contains(search));

        if (filter == "human")
            query = query.Where(c => c.IsHuman);

        int totalCount = await query.CountAsync();
        int safePage   = Math.Max(1, Math.Min(page, (int)Math.Ceiling(totalCount / (double)pageSize)));

        var countries = await query
            .OrderByDescending(c => c.MonthlyIncome)
            .ThenByDescending(c => c.TotalDevelopment)
            .Skip((safePage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new EconomyViewModel
        {
            SaveGameId  = saveId,
            GameDate    = save.GameDate,
            SearchTerm  = search,
            Page        = safePage,
            PageSize    = pageSize,
            TotalCount  = totalCount,
            TopCountries = countries.Select(c => new EconomyCountryData
            {
                Tag = c.Tag,
                Name = c.Name,
                PlayerName = c.IsHuman ? c.PlayerName : null,
                IsHuman = c.IsHuman,
                Treasury = c.Treasury,
                MonthlyIncome = c.MonthlyIncome,
                MonthlyExpenses = c.MonthlyExpenses,
                IncomeTax = c.IncomeTax,
                IncomeProduction = c.IncomeProduction,
                IncomeTrade = c.IncomeTrade,
                IncomeGold = c.IncomeGold,
                ExpenseArmy = c.ExpenseArmy,
                ExpenseNavy = c.ExpenseNavy,
                ExpenseBuildings = c.ExpenseBuildings,
                ExpenseOther = c.ExpenseOther
            }).ToList()
        };

        return View(vm);
    }

    /// <summary>
    /// AJAX-Endpoint: Gibt Chart-Daten als JSON zurück (kein Seitenreload).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ChartData(int saveId, string type = "income")
    {
        var countries = await _db.Countries
            .Where(c => c.SaveGameId == saveId && c.TotalDevelopment > 0)
            .OrderByDescending(c => c.MonthlyIncome)
            .Take(15)
            .ToListAsync();

        // Je nach Typ unterschiedliche Daten zurückgeben
        object data = type switch
        {
            "income" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                tax = countries.Select(c => Math.Round(c.IncomeTax, 1)).ToArray(),
                production = countries.Select(c => Math.Round(c.IncomeProduction, 1)).ToArray(),
                trade = countries.Select(c => Math.Round(c.IncomeTrade, 1)).ToArray(),
                gold = countries.Select(c => Math.Round(c.IncomeGold, 1)).ToArray()
            },
            "treasury" => new
            {
                labels = countries.Select(c => c.Name).ToArray(),
                values = countries.Select(c => Math.Round(c.Treasury, 0)).ToArray()
            },
            _ => new { }
        };

        return Json(data);
    }
}
