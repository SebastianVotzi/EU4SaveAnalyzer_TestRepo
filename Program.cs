using Microsoft.EntityFrameworkCore;
using EU4SaveAnalyzer.Data;
using EU4SaveAnalyzer.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Services registrieren
// ============================================================

// MVC mit Razor Views
builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson(); // Für besseres JSON-Handling

// Entity Framework Core mit SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=eu4saves.db"
    )
);

// EU4-spezifische Services (Scoped = pro Request neue Instanz)
builder.Services.AddScoped<EU4SaveParser>();

// Erhöhe das maximale Datei-Upload-Limit auf 500 MB
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 500 * 1024 * 1024; // 500 MB
});

// ============================================================
// App bauen
// ============================================================

var app = builder.Build();

// Datenbank automatisch erstellen/migrieren beim Start
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // Erstellt die DB falls sie nicht existiert
}

// Error Handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // Produktions-Fehlerseite
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Standard-Route: Home/Index
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
