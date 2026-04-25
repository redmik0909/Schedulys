using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class LocationsViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<Salle> Salles { get; } = new();

    public IReadOnlyList<string> Types { get; } = new[]
        { "Standard", "Labo informatique", "Amphithéâtre", "Gymnase", "Salle de dessin" };

    [ObservableProperty] private string _nomInput = "";
    [ObservableProperty] private string _capaciteInput = "";
    [ObservableProperty] private string _selectedType = "Standard";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private Salle? _selected;

    public LocationsViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var list = await _db.Salles.ListAsync();
        Salles.Clear();
        foreach (var s in list) Salles.Add(s);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NomInput)) return;
        int.TryParse(CapaciteInput, out var cap);
        await _db.Salles.CreateAsync(new Salle
        {
            Nom      = NomInput.Trim(),
            Capacite = cap,
            Type     = SelectedType
        });
        NomInput      = "";
        CapaciteInput = "";
        await LoadAsync();
    }

    [ObservableProperty] private string _erreur = "";

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        Erreur = "";
        try
        {
            var id = Selected.Id;
            Selected = null;
            await _db.Salles.DeleteAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Erreur = $"Impossible de supprimer : {ex.Message}";
        }
    }

    private bool HasSelection() => Selected is not null;
}
