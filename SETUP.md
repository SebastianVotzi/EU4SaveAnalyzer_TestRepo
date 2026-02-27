# EU4 Save Analyzer — Vollständige Setup-Anleitung
### ASP.NET Core 8 · EF Core · SQLite · Bootstrap 5 · Chart.js

---

## Inhaltsverzeichnis
1. [Voraussetzungen installieren](#1-voraussetzungen-installieren)
2. [Projekt entpacken](#2-projekt-entpacken)
3. [Lokal starten (Development)](#3-lokal-starten)
4. [Docker-Deployment](#4-docker-deployment)
5. [Erste Schritte in der App](#5-erste-schritte-in-der-app)
6. [Fehlerbehebung](#6-fehlerbehebung)
7. [Projektstruktur & Dokumentation](#7-projektstruktur--dokumentation)

---

## 1. Voraussetzungen installieren

### Option A: Lokal (ohne Docker)

**Schritt 1.1 — .NET 8 SDK installieren:**
1. Gehe zu: https://dotnet.microsoft.com/download/dotnet/8.0
2. Wähle „.NET 8.0 SDK" (nicht nur Runtime) für dein Betriebssystem
3. Installer ausführen und Standardoptionen akzeptieren
4. Terminal öffnen und prüfen:
   ```
   dotnet --version
   ```
   Muss `8.0.xxx` ausgeben. Wenn nicht → PC neu starten.

**Schritt 1.2 — Git (optional, nur für Klonen):**
Wenn du das Projekt von GitHub klonen willst:
- Windows: https://git-scm.com/download/win
- macOS: `brew install git`
- Linux: `sudo apt install git`

### Option B: Mit Docker

**Docker Desktop installieren:**
1. Gehe zu: https://www.docker.com/products/docker-desktop
2. Für Windows: WSL2-Backend aktivieren (wird beim Setup vorgeschlagen)
3. Nach Installation Docker Desktop starten
4. Prüfen:
   ```
   docker --version
   docker-compose --version
   ```

---

## 2. Projekt entpacken

**Schritt 2.1 — ZIP entpacken:**

*Windows:*
1. Rechtsklick auf `EU4SaveAnalyzer.zip`
2. „Alle extrahieren..." → Ordner wählen z.B. `C:\Projekte\EU4SaveAnalyzer`
3. Auf „Extrahieren" klicken

*macOS / Linux:*
```bash
unzip EU4SaveAnalyzer.zip -d ~/Projekte/EU4SaveAnalyzer
```

**Schritt 2.2 — Ordnerstruktur prüfen:**
Nach dem Entpacken sollte folgendes sichtbar sein:
```
EU4SaveAnalyzer/
├── EU4SaveAnalyzer.csproj   ← Projektdatei (wichtig!)
├── Program.cs
├── Controllers/
├── Models/
├── Views/
├── Dockerfile
└── docker-compose.yml
```

---

## 3. Lokal starten

> **Voraussetzung:** .NET 8 SDK ist installiert (Schritt 1.1)

**Schritt 3.1 — Terminal im Projektordner öffnen:**

*Windows:*
- Im Explorer in den `EU4SaveAnalyzer`-Ordner navigieren (wo die `.csproj`-Datei liegt)
- Shift + Rechtsklick → „PowerShell-Fenster hier öffnen"
  oder: Adressleiste anklicken, `cmd` tippen, Enter

*macOS:*
- Finder → Ordner → Rechtsklick → „Neues Terminal bei Ordner"

*Linux:*
```bash
cd ~/Projekte/EU4SaveAnalyzer/EU4SaveAnalyzer
```

**Schritt 3.2 — NuGet-Pakete wiederherstellen:**
```bash
dotnet restore
```
Ausgabe: `Restore completed` (dauert beim ersten Mal 10-30 Sekunden)

**Schritt 3.3 — Anwendung starten:**
```bash
dotnet run
```

Erwartete Ausgabe:
```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

**Schritt 3.4 — Browser öffnen:**
- → http://localhost:5000

Die SQLite-Datenbankdatei `eu4saves.db` wird automatisch beim ersten Start
im Projektordner erstellt. Keine manuelle Datenbank-Einrichtung nötig.

**Anwendung beenden:** `Ctrl + C` im Terminal

---

### Optionale Schnellstart-Befehle (alles in einem)

*Windows PowerShell:*
```powershell
cd C:\Pfad\zu\EU4SaveAnalyzer; dotnet run
```

*macOS / Linux:*
```bash
cd ~/Pfad/zu/EU4SaveAnalyzer && dotnet run
```

---

## 4. Docker-Deployment

> **Voraussetzung:** Docker Desktop läuft (Schritt 1.2)

**Schritt 4.1 — Terminal im Projektordner öffnen** (wie in Schritt 3.1)

**Schritt 4.2 — Container bauen und starten:**
```bash
docker-compose up --build
```

Beim ersten Aufruf wird das Image gebaut (dauert 1-3 Minuten).
Folgende Ausgabe bedeutet Erfolg:
```
eu4-save-analyzer  | Now listening on: http://[::]:8080
```

**Schritt 4.3 — Browser öffnen:**
- → http://localhost:8080

**Wichtige Docker-Befehle:**
```bash
# Im Hintergrund laufen lassen (kein Terminal blockiert):
docker-compose up --build -d

# Logs anzeigen:
docker-compose logs -f

# Stoppen:
docker-compose down

# Datenbank löschen und neu beginnen:
docker-compose down -v
```

**Datenpersistenz:** Die SQLite-Datenbankdatei liegt im Docker-Volume `eu4_data`.
Beim `docker-compose down` bleibt sie erhalten. Nur `docker-compose down -v` löscht sie.

---

## 5. Erste Schritte in der App

**5.1 — EU4 Save-Datei finden:**

*Windows:*
```
C:\Users\DEIN_NAME\Documents\Paradox Interactive\Europa Universalis IV\save games\
```

*macOS:*
```
~/Documents/Paradox Interactive/Europa Universalis IV/save games/
```

*Linux (Steam):*
```
~/.local/share/Paradox Interactive/Europa Universalis IV/save games/
```

Dateiendung: `.eu4`

> ⚠️ **Ironman-Saves funktionieren nicht** (binäres Format).
> Nur normale Saves (ohne Ironman-Modus gestartet) sind lesbar.
> Komprimierte Saves (ZIP-Format) werden automatisch erkannt und entpackt.

**5.2 — Save hochladen:**
1. http://localhost:5000 öffnen
2. „Klicken oder Datei hierher ziehen" → `.eu4`-Datei auswählen
3. „Analysieren" klicken
4. Warten (je nach Dateigröße 2-15 Sekunden)
5. Erfolgsmeldung: „Save erfolgreich geladen! X Nationen, Y Kriege gefunden."

**5.3 — Dashboards erkunden:**
Nach dem Upload auf das Balkendiagramm-Symbol in der Save-Liste klicken,
oder direkt in der Navbar navigieren:

| Navigation | Inhalt |
|-----------|--------|
| Wirtschaft | Einnahmen, Treasury, Profit |
| Militär | Armeegröße, Force Limit, Manpower |
| Spending | Ausgaben aufgeteilt nach Kategorien |
| Mana | ADM/DIP/MIL Ausgaben-Analyse |
| Kriege | Aktive und beendete Kriege |
| Ranking | Alle Nationen sortierbar vergleichen |
| **Spieler** | **Nur menschliche Spieler vergleichen** |

**5.4 — Spieler-Vergleich (Multiplayer):**
Der Spieler-Vergleich funktioniert am besten mit einem Multiplayer-Save
(mehrere menschliche Spieler). Bei Singleplayer-Saves wird nur eine Person angezeigt.

1. In der Navbar „Spieler" anklicken
2. Checkboxen: gewünschte Spieler anhaken → „Vergleich aktualisieren"
3. Chart-Typ umschalten: Wirtschaft / Militär / Mana / Spending / Herrscher
4. 🥇 in der Tabelle markiert den besten Wert pro Zeile

---

## 6. Fehlerbehebung

### „dotnet: command not found"
→ .NET SDK ist nicht im PATH. Terminal neu öffnen nach Installation.
Windows: PC neu starten.

### Port bereits belegt
```bash
# Anderen Port verwenden:
dotnet run --urls http://localhost:5050
```

### „Ironman/Binär-Saves werden nicht unterstützt"
→ Im EU4-Hauptmenü „Einstellungen" → Ironman-Modus deaktivieren und einen neuen Save erstellen.

### „Die Datei ist zu groß (max. 500 MB)"
→ Große Late-Game-Saves können über 500 MB sein. In `Program.cs` Zeile 20
`500 * 1024 * 1024` auf einen höheren Wert setzen.

### Charts werden nicht angezeigt
→ JavaScript-Fehler im Browser prüfen (F12 → Console).
Häufig: Ad-Blocker blockiert CDN-Links. CDN-URLs sind:
- `cdn.jsdelivr.net` (Bootstrap, Chart.js, Bootstrap Icons)

### Docker: „port is already allocated"
→ Port 8080 ist belegt. In `docker-compose.yml` `"8080:8080"` auf `"8081:8080"` ändern.

---

## 7. Projektstruktur & Dokumentation

Jede Datei enthält vollständige XML-Dokumentationskommentare (`///`).
Diese sind in Visual Studio / Rider über IntelliSense sichtbar.

```
EU4SaveAnalyzer/
│
├── Program.cs                         # Einstiegspunkt: DI, Middleware, DB-Initialisierung
│
├── Controllers/
│   ├── HomeController.cs              # Upload, Liste, Löschen von Saves
│   ├── EconomyController.cs           # Wirtschafts-Dashboard + AJAX ChartData
│   ├── DashboardControllers.cs        # Military, Spending, Mana, Wars, Ranking
│   └── PlayerComparisonController.cs  # Spieler-Vergleich + AJAX ChartData
│
├── Models/                            # EF Core Entitätsklassen (= Datenbanktabellen)
│   ├── SaveGame.cs                    # Tabelle: SaveGames
│   ├── Country.cs                     # Tabelle: Countries (alle Nationen)
│   └── War.cs                         # Tabelle: Wars
│
├── Data/
│   └── AppDbContext.cs                # EF Core DbContext: Tabellen + Beziehungen
│
├── Services/
│   ├── ClausewitzParser.cs            # Generischer EU4-Textformat-Parser
│   └── EU4SaveParser.cs               # EU4-spezifische Datenextraktion
│
├── ViewModels/
│   ├── ViewModels.cs                  # ViewModels für alle 6 Standard-Dashboards
│   └── PlayerComparisonViewModel.cs   # ViewModel für Spieler-Vergleich
│
├── Views/
│   ├── Shared/_Layout.cshtml          # Haupt-Layout (Navbar, Footer)
│   ├── Home/Index.cshtml              # Upload-Seite
│   ├── Economy/Index.cshtml           # Wirtschafts-Dashboard
│   ├── Military/Index.cshtml          # Militär-Dashboard
│   ├── Spending/Index.cshtml          # Spending-Dashboard
│   ├── Mana/Index.cshtml              # Mana-Dashboard
│   ├── Wars/Index.cshtml              # Kriegs-Dashboard
│   ├── Ranking/Index.cshtml           # Nationen-Ranking
│   └── PlayerComparison/Index.cshtml  # Spieler-Vergleich (neu)
│
├── wwwroot/css/site.css               # Dark-Theme CSS
├── appsettings.json                   # Konfiguration (DB-Pfad)
├── Dockerfile                         # Docker Build-Konfiguration
└── docker-compose.yml                 # Docker Compose (Container + Volume)
```

### Datenfluss beim Upload

```
Browser (Dateiupload)
    ↓
HomeController.Upload()
    ↓ öffnet Stream
EU4SaveParser.ParseAsync()
    ↓ erkennt ZIP oder Text
ClausewitzParser.Parse()         ← Liest Clausewitz-Textformat in Dictionary
    ↓ gibt Dictionary zurück
EU4SaveParser (ParseCountries, ParseWars)  ← Mappt auf C#-Models
    ↓ gibt (SaveGame, Countries[], Wars[]) zurück
AppDbContext.SaveChangesAsync()  ← Persistiert in SQLite
    ↓
Redirect → Home/Index
```

### Datenbankschema (vereinfacht)

```
SaveGames
  Id (PK)  |  FileName  |  GameDate  |  PlayerTag  |  UploadedAt

Countries
  Id (PK)  |  SaveGameId (FK→SaveGames)  |  Tag  |  Name  |  IsHuman  |
  PlayerName  |  Treasury  |  MonthlyIncome  |  ArmySize  |  ...

Wars
  Id (PK)  |  SaveGameId (FK→SaveGames)  |  Name  |  StartDate  |
  IsActive  |  AttackerTags  |  DefenderTags  |  Outcome  |  ...
```
