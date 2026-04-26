using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        "Direction"
    };

    [ObservableProperty] private string _nomInput     = "";
    [ObservableProperty] private string _selectedRole = "Enseignant";
    [ObservableProperty] private string _minutesInput = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveAllQuotasCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private ProfDisplay? _selected;

    public bool HasSelection => Selected is not null;

    partial void OnSelectedChanged(ProfDisplay? value)
    {
        _ = LoadQuotasParJourAsync(value);
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

        for (int j = 0; j <= 9; j++)
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

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NomInput)) return;

        var profId = await _db.Profs.CreateAsync(new Prof
        {
            Nom  = NomInput.Trim(),
            Role = SelectedRole
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

    [ObservableProperty] private string _erreur = "";
}
