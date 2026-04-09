using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Data;
using EU4SaveAnalyzer.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Services registrieren
// ============================================================

// MVC mit Razor Views
builder.Services.AddControllersWithViews();

// Entity Framework Core mit SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=eu4saves.db"
    )
);

// EU4-spezifische Services (Scoped = pro Request neue Instanz)
builder.Services.AddScoped<EU4SaveParser>();

// Maximales Datei-Upload-Limit auf 500 MB erhöhen
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024;
});

// ============================================================
// App bauen
// ============================================================

var app = builder.Build();

// Datenbank automatisch erstellen beim Start
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Error Handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Standard-Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
