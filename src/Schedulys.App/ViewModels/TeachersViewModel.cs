using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed class ProfDisplay(Prof prof, int minutesJour0)
{
    public Prof   Prof           { get; } = prof;
    public string Nom            => Prof.Nom;
    public string Role           => Prof.Role;
    public int    MinutesParJour { get; set; } = minutesJour0;
    public string MinutesLabel   => MinutesParJour > 0 ? $"{MinutesParJour} min/j" : "—";
}

public sealed class QuotaJourDisplay
{
    public int    JourCycle     { get; init; }
    public int    ProfId        { get; init; }
    public string AnneeScolaire { get; init; } = "";
    public string Label         => JourCycle == 0 ? "Moy." : $"Jour {JourCycle}";
    public string MinutesInput  { get; set; } = "";
}

public sealed partial class TeachersViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<ProfDisplay>      Profs         { get; } = new();
    public ObservableCollection<QuotaJourDisplay> QuotasParJour { get; } = new();

    public IReadOnlyList<string> Roles { get; } = new[]
    {
        "Enseignant",
        "Ortho. SAI",
        "Coordonnateur EHDAA",
        "Surveillant"
    };

    [ObservableProperty] private string _nomInput     = "";
    [ObservableProperty] private string _selectedRole = "Enseignant";
    [ObservableProperty] private string _minutesInput = "";
    [ObservableProperty] private string _erreur       = "";
    [ObservableProperty] private string _importMessage = "";
    [ObservableProperty] private bool   _importEnCours;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAllQuotasCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(AddButtonLabel))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    private ProfDisplay? _selected;

    public bool   HasSelection  => Selected is not null;
    public string AddButtonLabel => Selected is not null ? "Modifier" : "Ajouter";
    public string FormTitle      => Selected is not null ? "Modifier le membre" : "Ajouter du personnel";

    partial void OnSelectedChanged(ProfDisplay? value)
    {
        _ = LoadQuotasParJourAsync(value);

        if (value is not null)
        {
            NomInput     = value.Nom;
            SelectedRole = value.Role;
        }
        else
        {
            NomInput     = "";
            SelectedRole = "Enseignant";
            MinutesInput = "";
        }
    }

    public TeachersViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var profs  = await _db.Profs.ListAsync();
        var quotas = await _db.Quotas.ListAsync();

        var savedId = Selected?.Prof.Id;
        Profs.Clear();
        foreach (var p in profs)
        {
            var q = quotas.FirstOrDefault(x => x.ProfId == p.Id && x.JourCycle == 0);
            Profs.Add(new ProfDisplay(p, q?.MinutesMax ?? 0));
        }

        if (savedId.HasValue)
            Selected = Profs.FirstOrDefault(p => p.Prof.Id == savedId);
    }

    private async Task LoadQuotasParJourAsync(ProfDisplay? prof)
    {
        QuotasParJour.Clear();
        if (prof is null) return;

        var quotas = await _db.Quotas.GetAllByProfAsync(prof.Prof.Id, AppConstants.AnneeScolaire);

        // Ligne 0 = moyenne
        var moy = quotas.FirstOrDefault(x => x.JourCycle == 0);
        QuotasParJour.Add(new QuotaJourDisplay
        {
            JourCycle     = 0,
            ProfId        = prof.Prof.Id,
            AnneeScolaire = AppConstants.AnneeScolaire,
            MinutesInput  = moy?.MinutesMax > 0 ? moy.MinutesMax.ToString() : ""
        });

        // Lignes 1..N selon les données réelles (flexible : 8, 9, 10 jours…)
        var maxJour = quotas.Where(q => q.JourCycle > 0)
                            .Select(q => q.JourCycle)
                            .DefaultIfEmpty(0)
                            .Max();

        for (int j = 1; j <= maxJour; j++)
        {
            var q = quotas.FirstOrDefault(x => x.JourCycle == j);
            QuotasParJour.Add(new QuotaJourDisplay
            {
                JourCycle     = j,
                ProfId        = prof.Prof.Id,
                AnneeScolaire = AppConstants.AnneeScolaire,
                MinutesInput  = q?.MinutesMax > 0 ? q.MinutesMax.ToString() : ""
            });
        }
    }

    // ── Ajout / Modification ─────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NomInput)) return;

        if (Selected is not null)
        {
            // Mode modification
            Selected.Prof.Nom  = NomInput.Trim();
            Selected.Prof.Role = SelectedRole;
            await _db.Profs.UpdateAsync(Selected.Prof);
            await LoadAsync();
            return;
        }

        // Mode ajout
        var profId = await _db.Profs.CreateAsync(new Prof
        {
            Nom   = NomInput.Trim(),
            Role  = SelectedRole,
            Annee = AppConstants.AnneeScolaire
        });

        if (int.TryParse(MinutesInput, out var min) && min > 0)
        {
            await _db.Quotas.UpsertAsync(new QuotaMinutes
            {
                ProfId        = profId,
                JourCycle     = 0,
                MinutesMax    = min,
                AnneeScolaire = AppConstants.AnneeScolaire
            });
        }

        NomInput     = "";
        MinutesInput = "";
        await LoadAsync();
    }

    // ── Import HTML ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ImporterHtmlAsync()
    {
        ImportMessage = "";
        Erreur        = "";

        var dlg = new OpenFileDialog
        {
            Title      = "Sélectionner le fichier HTML des enseignants",
            Filter     = "Fichiers HTML (*.html;*.htm)|*.html;*.htm",
            DefaultExt = ".html"
        };
        if (dlg.ShowDialog() != true) return;

        ImportEnCours = true;
        try
        {
            var html     = await File.ReadAllTextAsync(dlg.FileName);
            var imported = await ParseAndImportHtmlAsync(html);
            ImportMessage = $"✓ {imported} enseignant(s) importé(s) / mis à jour";
        }
        catch (Exception ex)
        {
            Erreur = $"Erreur import : {ex.Message}";
        }
        finally
        {
            ImportEnCours = false;
        }
    }

    private async Task<int> ParseAndImportHtmlAsync(string html)
    {
        html = Regex.Replace(html, @"<!--.*?-->",           "",  RegexOptions.Singleline);
        html = Regex.Replace(html, @"<script.*?</script>", "",  RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style.*?</style>",   "",  RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var rowRegex  = new Regex(@"<tr\b[^>]*>(.*?)</tr>",        RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var cellRegex = new Regex(@"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var tagRegex  = new Regex(@"<[^>]+>");

        string StripTags(string s) => tagRegex.Replace(s, "").Trim();

        var existingProfs = await _db.Profs.ListAsync();
        var profDict      = existingProfs.ToDictionary(
            p => p.Nom.ToLowerInvariant(),
            p => p,
            StringComparer.OrdinalIgnoreCase);

        int count = 0;

        foreach (Match rowMatch in rowRegex.Matches(html))
        {
            var cells = cellRegex.Matches(rowMatch.Groups[1].Value);
            if (cells.Count < 2) continue;

            var rawName = StripTags(cells[0].Groups[1].Value);
            if (string.IsNullOrWhiteSpace(rawName)) continue;

            // Détecte dynamiquement le nombre de jours (cols numériques consécutives après le nom)
            var jours = new List<int>();
            for (int i = 1; i < cells.Count; i++)
            {
                var raw = StripTags(cells[i].Groups[1].Value);
                if (int.TryParse(raw, out var min))
                    jours.Add(min);
                else
                    break; // arrête à la première cellule non numérique
            }
            if (jours.Count == 0) continue;

            var titleName = ToTitleCase(rawName);
            var key       = titleName.ToLowerInvariant();

            int profId;
            if (profDict.TryGetValue(key, out var existing))
            {
                profId = existing.Id;
            }
            else
            {
                profId = await _db.Profs.CreateAsync(new Prof
                {
                    Nom   = titleName,
                    Role  = "Enseignant",
                    Annee = AppConstants.AnneeScolaire
                });
                profDict[key] = new Prof { Id = profId, Nom = titleName, Role = "Enseignant" };
            }

            // Moyenne + quotas par jour (flexible : autant de jours que détectés)
            var moyenne = (int)Math.Round(jours.Average());
            await _db.Quotas.UpsertAsync(new QuotaMinutes
            {
                ProfId = profId, JourCycle = 0, MinutesMax = moyenne,
                AnneeScolaire = AppConstants.AnneeScolaire
            });
            for (int j = 1; j <= jours.Count; j++)
                await _db.Quotas.UpsertAsync(new QuotaMinutes
                {
                    ProfId = profId, JourCycle = j, MinutesMax = jours[j - 1],
                    AnneeScolaire = AppConstants.AnneeScolaire
                });

            count++;
        }

        await LoadAsync();
        return count;
    }

    private static string ToTitleCase(string s)
    {
        var sb  = new StringBuilder(s.Length);
        bool cap = true;
        foreach (char c in s.ToLower())
        {
            sb.Append(cap && char.IsLetter(c) ? char.ToUpper(c) : c);
            cap = c is ' ' or '-';
        }
        return sb.ToString();
    }

    // ── Sauvegarde quotas ─────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveAllQuotasAsync()
    {
        if (Selected is null) return;
        foreach (var qd in QuotasParJour)
        {
            if (!int.TryParse(qd.MinutesInput, out var min) || min < 0) continue;
            await _db.Quotas.UpsertAsync(new QuotaMinutes
            {
                ProfId        = qd.ProfId,
                JourCycle     = qd.JourCycle,
                MinutesMax    = min,
                AnneeScolaire = qd.AnneeScolaire
            });
        }
        await LoadAsync();
    }

    // ── Suppression ───────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        Erreur = "";
        try
        {
            var id = Selected.Prof.Id;
            Selected = null;
            await _db.Profs.DeleteAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Erreur = $"Impossible de supprimer : {ex.Message}";
        }
    }
}
