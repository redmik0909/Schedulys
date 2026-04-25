using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class TeachersViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<Prof> Profs { get; } = new();

    public IReadOnlyList<string> Roles { get; } = new[] { "Enseignant", "Surveillant" };

    [ObservableProperty] private string _nomInput = "";
    [ObservableProperty] private string _selectedRole = "Enseignant";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private Prof? _selected;

    public TeachersViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _db.Profs.ListAsync();
        Profs.Clear();
        foreach (var p in list) Profs.Add(p);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NomInput)) return;
        await _db.Profs.CreateAsync(new Prof { Nom = NomInput.Trim(), Role = SelectedRole });
        NomInput = "";
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        await _db.Profs.DeleteAsync(Selected.Id);
        Selected = null;
        await LoadAsync();
    }

    private bool HasSelection() => Selected is not null;
}
