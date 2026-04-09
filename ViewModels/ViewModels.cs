namespace EU4SaveAnalyzer.ViewModels;

/// <summary>ViewModel für die Home/Upload-Seite.</summary>
public class HomeViewModel
{
    public List<SaveGameListItem> SaveGames { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public class SaveGameListItem
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string GameDate { get; set; } = string.Empty;
    public string PlayerTag { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public int CountryCount { get; set; }
}

/// <summary>ViewModel für das Wirtschafts-Dashboard mit Pagination.</summary>
public class EconomyViewModel
{
    public int SaveGameId { get; set; }
    public string GameDate { get; set; } = string.Empty;
    public List<EconomyCountryData> TopCountries { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class EconomyCountryData
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PlayerName { get; set; }
    public bool IsHuman { get; set; }
    public double Treasury { get; set; }
    public double MonthlyIncome { get; set; }
    public double MonthlyExpenses { get; set; }
    public double IncomeTax { get; set; }
    public double IncomeProduction { get; set; }
    public double IncomeTrade { get; set; }
    public double IncomeGold { get; set; }
    public double ExpenseArmy { get; set; }
    public double ExpenseNavy { get; set; }
    public double ExpenseBuildings { get; set; }
    public double ExpenseOther { get; set; }
    public double Profit => MonthlyIncome - MonthlyExpenses;
    /// <summary>Formatierter Profit-String mit Vorzeichen (z.B. "+12.3" oder "-5.6")</summary>
    public string ProfitDisplay => (Profit >= 0 ? "+" : "") + Profit.ToString("N1");
}

/// <summary>ViewModel für das Militär-Dashboard mit Pagination.</summary>
public class MilitaryViewModel
{
    public int SaveGameId { get; set; }
    public string GameDate { get; set; } = string.Empty;
    public List<MilitaryCountryData> Countries { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class MilitaryCountryData
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PlayerName { get; set; }
    public bool IsHuman { get; set; }
    public int ArmySize { get; set; }
    public int ForceLimit { get; set; }
    public double Manpower { get; set; }
    public double MaxManpower { get; set; }
    public int NavySize { get; set; }
    public int NavalForceLimit { get; set; }
    // Wie viel % des Force Limits genutzt wird
    public double ForceLimitUsage => ForceLimit > 0 ? (ArmySize / (double)ForceLimit) * 100 : 0;
}

/// <summary>ViewModel für das Ausgaben-Dashboard mit Pagination.</summary>
public class SpendingViewModel
{
    public int SaveGameId { get; set; }
    public string GameDate { get; set; } = string.Empty;
    public List<SpendingCountryData> Countries { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class SpendingCountryData
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PlayerName { get; set; }
    public bool IsHuman { get; set; }
    public double ExpenseArmy { get; set; }
    public double ExpenseNavy { get; set; }
    public double ExpenseBuildings { get; set; }
    public double ExpenseOther { get; set; }
    public double TotalExpenses => ExpenseArmy + ExpenseNavy + ExpenseBuildings + ExpenseOther;
    public double ArmyPercent => TotalExpenses > 0 ? (ExpenseArmy / TotalExpenses) * 100 : 0;
    public double NavyPercent => TotalExpenses > 0 ? (ExpenseNavy / TotalExpenses) * 100 : 0;
    public double BuildingsPercent => TotalExpenses > 0 ? (ExpenseBuildings / TotalExpenses) * 100 : 0;
    public double OtherPercent => TotalExpenses > 0 ? (ExpenseOther / TotalExpenses) * 100 : 0;
}

/// <summary>ViewModel für das Mana-Dashboard mit Pagination.</summary>
public class ManaViewModel
{
    public int SaveGameId { get; set; }
    public string GameDate { get; set; } = string.Empty;
    public List<ManaCountryData> Countries { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class ManaCountryData
{
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PlayerName { get; set; }
    public bool IsHuman { get; set; }

    // Generiert
    public int TotalAdmGenerated { get; set; }
    public int TotalDipGenerated { get; set; }
    public int TotalMilGenerated { get; set; }

    // Aktueller Vorrat
    public int AdmPower { get; set; }
    public int DipPower { get; set; }
    public int MilPower { get; set; }

    // Ausgaben ADM
    public int AdmSpentTech { get; set; }
    public int AdmSpentIdeas { get; set; }
    public int AdmSpentStability { get; set; }
    public int AdmSpentOther { get; set; }
    public int TotalAdmSpent => AdmSpentTech + AdmSpentIdeas + AdmSpentStability + AdmSpentOther;

    // Ausgaben DIP
    public int DipSpentTech { get; set; }
    public int DipSpentIdeas { get; set; }
    public int DipSpentDiplomacy { get; set; }
    public int DipSpentOther { get; set; }
    public int TotalDipSpent => DipSpentTech + DipSpentIdeas + DipSpentDiplomacy + DipSpentOther;

    // Ausgaben MIL
    public int MilSpentTech { get; set; }
    public int MilSpentIdeas { get; set; }
    public int MilSpentLeaders { get; set; }
    public int MilSpentOther { get; set; }
    public int TotalMilSpent => MilSpentTech + MilSpentIdeas + MilSpentLeaders + MilSpentOther;
}

/// <summary>ViewModel für das Kriegs-Dashboard mit Pagination für vergangene Kriege.</summary>
public class WarsViewModel
{
    public int SaveGameId { get; set; }
    public string GameDate { get; set; } = string.Empty;
    public List<WarData> ActiveWars { get; set; } = new();
    public List<WarData> PreviousWars { get; set; } = new();
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int TotalPreviousWars { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalPreviousWars / (double)PageSize);
    public bool HasPrevPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class WarData
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string AttackerTags { get; set; } = string.Empty;
    public string DefenderTags { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public int AttackerLosses { get; set; }
    public int DefenderLosses { get; set; }
    public double WarScore { get; set; }
    public int TotalLosses => AttackerLosses + DefenderLosses;

    public string OutcomeDisplay => Outcome switch
    {
        "attacker_win" => "Angreifer gewonnen",
        "defender_win" => "Verteidiger gewonnen",
        "draw" => "Unentschieden",
        "ongoing" => "Laufend",
        _ => "Unbekannt"
    };

    public string OutcomeBadgeClass => Outcome switch
    {
        "attacker_win" => "badge-success",
        "defender_win" => "badge-info",
        "draw" => "badge-warning",
        "ongoing" => "badge-danger",
        _ => "badge-secondary"
    };
}

/// <summary>ViewModel für das Nationen-Ranking mit Pagination.</summary>
public class RankingViewModel
{
    public int SaveGameId { get; set; }
    public string GameDate { get; set; } = string.Empty;
    public List<RankingEntry> Rankings { get; set; } = new();
    public string SortBy { get; set; } = "army";
    public string? SearchTerm { get; set; }
    /// <summary>Aktuelle Seite (1-basiert)</summary>
    public int Page { get; set; } = 1;
    /// <summary>Einträge pro Seite</summary>
    public int PageSize { get; set; } = 50;
    /// <summary>Gesamtanzahl der gefilterten Einträge (vor Pagination)</summary>
    public int TotalCount { get; set; }
    /// <summary>Gesamtanzahl der Seiten</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class RankingEntry
{
    public int Rank { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PlayerName { get; set; }
    public bool IsHuman { get; set; }
    public int ArmySize { get; set; }
    public int ForceLimit { get; set; }
    public double Manpower { get; set; }
    public double MonthlyIncome { get; set; }
    public int ProvinceCount { get; set; }
    public double RulerAdm { get; set; }
    public double RulerDip { get; set; }
    public double RulerMil { get; set; }
    public double RulerAvg { get; set; }
    public int TotalAdmGenerated { get; set; }
    public int TotalDipGenerated { get; set; }
    public int TotalMilGenerated { get; set; }
    public int TotalManaGenerated => TotalAdmGenerated + TotalDipGenerated + TotalMilGenerated;
    public int TotalDevelopment { get; set; }
    public int DevClicksAdm { get; set; }
    public int DevClicksDip { get; set; }
    public int DevClicksMil { get; set; }
    public int TotalDevClicks => DevClicksAdm + DevClicksDip + DevClicksMil;
}
