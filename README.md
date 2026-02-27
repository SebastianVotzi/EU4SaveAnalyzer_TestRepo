# EU4 Save Analyzer
### ASP.NET Core MVC · Entity Framework Core · SQLite · Chart.js

Schulprojekt für die 4IT SEW - Europa Universalis IV Save-Datei Analyse-Tool.

---

## 🚀 Schnellstart (lokal)

```bash
# 1. Abhängigkeiten installieren
dotnet restore

# 2. Datenbank erstellen (automatisch beim ersten Start)
dotnet run

# 3. Browser öffnen
http://localhost:5000
```

## 🐳 Docker Deployment

```bash
# Option A: Docker Compose (empfohlen)
docker-compose up --build

# Option B: Manuell
docker build -t eu4analyzer .
docker run -p 8080:8080 -v eu4data:/app/data eu4analyzer
```
Dann: http://localhost:8080

---

## 📁 Projektstruktur

```
EU4SaveAnalyzer/
├── Controllers/
│   ├── HomeController.cs         # Upload & Liste
│   ├── EconomyController.cs      # Wirtschafts-Dashboard
│   └── DashboardControllers.cs   # Military, Spending, Mana, Wars, Ranking
├── Models/
│   ├── SaveGame.cs               # Save-Metadaten
│   ├── Country.cs                # Nationen-Daten
│   └── War.cs                    # Kriegs-Daten
├── Services/
│   ├── ClausewitzParser.cs       # EU4 Text-Format Parser
│   └── EU4SaveParser.cs          # EU4-spezifische Datenextraktion
├── Data/
│   └── AppDbContext.cs           # EF Core DbContext
├── ViewModels/
│   └── ViewModels.cs             # Alle View-Modelle
├── Views/
│   ├── Home/Index.cshtml         # Seite 1: Upload
│   ├── Economy/Index.cshtml      # Seite 2: Wirtschaft
│   ├── Military/Index.cshtml     # Seite 3: Militär
│   ├── Spending/Index.cshtml     # Seite 4: Ausgaben
│   ├── Mana/Index.cshtml         # Seite 5: Mana
│   ├── Wars/Index.cshtml         # Seite 6: Kriege
│   └── Ranking/Index.cshtml      # Seite 7: Ranking
├── wwwroot/css/site.css          # Dark Theme CSS
├── Program.cs                    # DI + Middleware Setup
├── appsettings.json              # Konfiguration
├── Dockerfile                    # Docker Build
└── docker-compose.yml            # Docker Compose
```

---

## 📊 Dashboard-Seiten

| Seite | Inhalt |
|-------|--------|
| **Upload** | Save-Datei hochladen (.eu4), Drag & Drop |
| **Wirtschaft** | Monatliches Einkommen, Treasury, Einnahmen-Aufschlüsselung |
| **Militär** | Armeegröße, Force Limit, Manpower, Marine |
| **Spending** | Ausgaben: Armee, Marine, Gebäude, Sonstiges |
| **Mana** | ADM/DIP/MIL-Ausgaben nach Kategorien (Techs, Ideen, etc.) |
| **Kriege** | Aktive und vergangene Kriege mit Verluststatistiken |
| **Ranking** | Alle Nationen sortierbar nach 8 Kriterien + Radar-Chart |

---

## 🗄️ Datenbankmodell

```
SaveGame (1)──────< Country (n)
    │
    └──────< War (n)
```

**SaveGame**: Dateiname, Spieldatum, Spieler-Tag, Upload-Zeitpunkt  
**Country**: Tag, Name, Spielername, alle Wirtschafts-/Militär-/Mana-Stats  
**War**: Name, Start/Ende, Parteien, Ausgang, Verluste  

---

## ⚙️ Technische Details

- **ASP.NET Core 8** MVC
- **Entity Framework Core 8** + SQLite
- **AJAX / Fetch API** für Chart-Daten ohne Seitenreload
- **Chart.js 4** für interaktive Charts (Bar, Doughnut, Radar)
- **Bootstrap 5** Responsive Design
- **Clausewitz-Parser**: Eigener rekursiver Parser für EU4-Format
- **ZIP-Support**: Automatisches Entpacken komprimierter Saves

### Unterstützte Save-Typen
- ✅ Normale EU4 Saves (.eu4 als Text)
- ✅ Komprimierte EU4 Saves (.eu4 als ZIP)
- ❌ Ironman Saves (binär, nicht unterstützt)

---

## 🔧 Anforderungen (Schulprojekt PDF)

| Kriterium | Status |
|-----------|--------|
| MVC-Pattern | ✅ Models, Controllers, Views |
| EF Core + Datenbank | ✅ SQLite mit EF Core 8 |
| Min. 6 Seiten | ✅ 7 Seiten |
| Fehlerbehandlung | ✅ Try-catch, Validierung, Alerts |
| Datenvalidierung | ✅ Server-seitig |
| Responsive Design | ✅ Bootstrap 5 |
| Docker | ✅ Dockerfile + docker-compose |
| Such-/Filterfunktion | ✅ Alle Dashboards haben Suche |
| AJAX | ✅ Alle Charts laden via Fetch API |
