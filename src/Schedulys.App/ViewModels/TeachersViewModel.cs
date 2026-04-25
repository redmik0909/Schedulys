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

public sealed class ProfDisplay(Prof prof, int minutesParJour)
{
    public Prof   Prof           { get; } = prof;
    public string Nom            => Prof.Nom;
    public string Role           => Prof.Role;
    public int    MinutesParJour { get; set; } = minutesParJour;
    public string MinutesLabel   => MinutesParJour > 0 ? $"{MinutesParJour} min/j" : "—";
}

public sealed partial class TeachersViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<ProfDisplay> Profs { get; } = new();

    public IReadOnlyList<string> Roles { get; } = new[]
    {
        "Enseignant",
        "Ortho. SAI",
        "Coordonnateur EHDAA",
        "Direction"
    };

    [ObservableProperty] private string      _nomInput         = "";
    [ObservableProperty] private string      _selectedRole     = "Enseignant";
    [ObservableProperty] private string      _minutesInput     = "";  // minutes/jour cibles

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveMinutesCommand))]
    private ProfDisplay? _selected;

    // Champ édition minutes pour la ligne sélectionnée
    [ObservableProperty] private string _minutesEditInput = "";

    partial void OnSelectedChanged(ProfDisplay? value)
    {
        MinutesEditInput = value?.MinutesParJour > 0
            ? value.MinutesParJour.ToString()
            : "";
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

        Profs.Clear();
        foreach (var p in profs)
        {
            var q = quotas.FirstOrDefault(x => x.ProfId == p.Id);
            Profs.Add(new ProfDisplay(p, q?.MinutesMax ?? 0));
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
                MinutesMax    = min,
                AnneeScolaire = "2025-2026"
            });
        }

        NomInput      = "";
        MinutesInput  = "";
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveMinutesAsync()
    {
        if (Selected is null) return;
        if (!int.TryParse(MinutesEditInput, out var min) || min <= 0) return;

        await _db.Quotas.UpsertAsync(new QuotaMinutes
        {
            ProfId        = Selected.Prof.Id,
            MinutesMax    = min,
            AnneeScolaire = "2025-2026"
        });

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

    public bool HasSelection() => Selected is not null;
}
