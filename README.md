# Schedulys

Application de bureau pour la gestion des examens et de la surveillance dans un établissement scolaire.  
Développée en C# / .NET 8 WPF, base de données SQLite locale.

## Fonctionnalités

- **Planification** — Création de sessions d'examen par date et jour du cycle (1–9), ajout de groupes et de rôles de surveillance
- **Surveillance** — Suivi des minutes de surveillance assignées vs quota par enseignant, par jour du cycle
- **Personnel** — Gestion des enseignants avec quotas de minutes distincts pour chacun des 9 jours du calendrier scolaire
- **Groupes / Classes** — Organisation par niveau (Sec 1–5), gestion de l'effectif
- **Épreuves** — Catalogue des épreuves avec durée, tiers-temps et indicateur ministériel
- **Locaux** — Gestion des salles avec capacité
- **Exports** — Exportation des données de planification
- **Licence** — Activation par clé, vérification d'expiration, mise à jour automatique

## Stack technique

| Composant | Technologie |
|-----------|-------------|
| UI | WPF (.NET 8), MaterialDesignInXaml 5.3 |
| Architecture | MVVM (CommunityToolkit.Mvvm) |
| Base de données | SQLite (Dapper) |
| Packaging | Inno Setup (installateur Windows) |
| Release | Script PowerShell automatisé (`release.ps1`) |

## Prérequis

- Windows 10/11
- .NET 8 SDK (développement uniquement — l'installateur est autonome)

## Lancer en développement

```bash
dotnet run --project src/Schedulys.App/Schedulys.App.csproj
```

## Créer une release

```powershell
.\release.ps1 -version 1.2
```

Le script met à jour la version, compile, génère l'installateur via Inno Setup, publie sur GitHub et upload le `.exe`.

## Structure du projet

```
src/
  Schedulys.Core/     — Modèles et interfaces
  Schedulys.Data/     — Repositories SQLite, migrations, DataSeeder
  Schedulys.App/      — WPF : Views, ViewModels, styles
```
