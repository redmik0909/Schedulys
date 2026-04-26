using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    public ObservableCollection<Prof>   Profs   { get; } = new();

    [ObservableProperty] private string _codeInput        = "";
    [ObservableProperty] private string _descriptionInput = "";
    [ObservableProperty] private string _effectifInput    = "";
    [ObservableProperty] private Prof?  _profInput;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private Classe? _selected;

    [ObservableProperty] private string _erreur = "";

    public GroupesViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var profs = await _db.Profs.ListAsync();
        Profs.Clear();
        foreach (var p in profs) Profs.Add(p);

        var list = await _db.Classes.ListAsync();
        Classes.Clear();
        foreach (var c in list.OrderBy(c => c.Code).ThenBy(c => c.Description))
            Classes.Add(c);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(CodeInput)) return;
        int.TryParse(EffectifInput, out var eff);
        await _db.Classes.CreateAsync(new Classe
        {
            Code        = CodeInput.Trim().ToUpperInvariant(),
            Description = DescriptionInput.Trim(),
            ProfId      = ProfInput?.Id ?? 0,
            Effectif    = eff,
            Nom         = CodeInput.Trim(),
            Annee       = AppConstants.AnneeScolaire
        });
        CodeInput        = "";
        DescriptionInput = "";
        EffectifInput    = "";
        ProfInput        = null;
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
