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

    [ObservableProperty] private string _nomInput      = "";
    [ObservableProperty] private string _capaciteInput = "";
    [ObservableProperty] private string _selectedType  = "Standard";
    [ObservableProperty] private string _erreur        = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(AddButtonLabel))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    private Salle? _selected;

    public bool   HasSelection  => Selected is not null;
    public string AddButtonLabel => Selected is not null ? "Modifier" : "Ajouter";
    public string FormTitle      => Selected is not null ? "Modifier le local" : "Ajouter un local";

    partial void OnSelectedChanged(Salle? value)
    {
        if (value is not null)
        {
            NomInput      = value.Nom;
            CapaciteInput = value.Capacite.ToString();
            SelectedType  = value.Type ?? "Standard";
        }
        else
        {
            NomInput      = "";
            CapaciteInput = "";
            SelectedType  = "Standard";
        }
    }

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

        if (Selected is not null)
        {
            // Mode modification
            Selected.Nom      = NomInput.Trim();
            Selected.Capacite = cap;
            Selected.Type     = SelectedType;
            await _db.Salles.UpdateAsync(Selected);
            await LoadAsync();
            return;
        }

        // Mode ajout
        await _db.Salles.CreateAsync(new Salle
        {
            Nom      = NomInput.Trim(),
            Capacite = cap,
            Type     = SelectedType,
            Annee    = AppConstants.AnneeScolaire
        });
        NomInput      = "";
        CapaciteInput = "";
        await LoadAsync();
    }

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
}
