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

    private async Task LoadAsync()
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

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        await _db.Classes.DeleteAsync(Selected.Id);
        Selected = null;
        await LoadAsync();
    }

    private bool HasSelection() => Selected is not null;
}
