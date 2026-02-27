# ============================================================
# EU4 Save Analyzer - Dockerfile
# Multi-Stage Build für minimales Image
# ============================================================

# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Projektdatei kopieren und Abhängigkeiten wiederherstellen
COPY EU4SaveAnalyzer.csproj .
RUN dotnet restore

# Quellcode kopieren und kompilieren
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime Image (kleiner, nur .NET Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Veröffentlichte Dateien kopieren
COPY --from=build /app/publish .

# Persistenz: SQLite-Datenbankdatei außerhalb des Containers
VOLUME ["/app/data"]

# Verbindungsstring auf Volume-Pfad setzen
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/eu4saves.db"
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Port freigeben
EXPOSE 8080

# Anwendung starten
ENTRYPOINT ["dotnet", "EU4SaveAnalyzer.dll"]
