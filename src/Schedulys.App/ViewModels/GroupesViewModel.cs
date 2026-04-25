using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class GroupesViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<Classe> Classes { get; } = new();

    [ObservableProperty] private string _nomInput = "";
    [ObservableProperty] private string _effectifInput = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private Classe? _selected;

    public GroupesViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var list = await _db.Classes.ListAsync();
        Classes.Clear();
        foreach (var c in list) Classes.Add(c);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NomInput)) return;
        int.TryParse(EffectifInput, out var eff);
        await _db.Classes.CreateAsync(new Classe
        {
            Nom      = NomInput.Trim(),
            Effectif = eff
        });
        NomInput      = "";
        EffectifInput = "";
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
            await _db.Classes.DeleteAsync(id);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Erreur = $"Impossible de supprimer : {ex.Message}";
        }
    }

    private bool HasSelection() => Selected is not null;
}
