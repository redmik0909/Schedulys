using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class ExamsViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<EpreuveDisplay> Epreuves { get; } = new();
    public ObservableCollection<Classe> ClassesDisponibles { get; } = new();

    [ObservableProperty] private string _nomInput = "";
    [ObservableProperty] private string _dureeInput = "120";
    [ObservableProperty] private Classe? _selectedClasse;
    [ObservableProperty] private bool _tiersTemps;
    [ObservableProperty] private bool _ministerielle;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    private EpreuveDisplay? _selected;

    public ExamsViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var classes  = await _db.Classes.ListAsync();
        var epreuves = await _db.Epreuves.ListAsync();

        ClassesDisponibles.Clear();
        foreach (var c in classes) ClassesDisponibles.Add(c);

        Epreuves.Clear();
        foreach (var e in epreuves)
        {
            var classe = classes.FirstOrDefault(c => c.Id == e.ClasseId);
            Epreuves.Add(new EpreuveDisplay(e, classe?.Nom ?? "—"));
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NomInput) || SelectedClasse is null) return;
        int.TryParse(DureeInput, out var duree);
        if (duree <= 0) duree = 60;

        await _db.Epreuves.CreateAsync(new Epreuve
        {
            Nom           = NomInput.Trim(),
            ClasseId      = SelectedClasse.Id,
            DureeMinutes  = duree,
            TiersTemps    = TiersTemps,
            Ministerielle = Ministerielle
        });

        NomInput      = "";
        DureeInput    = "120";
        TiersTemps    = false;
        Ministerielle = false;
        await LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task DeleteSelectedAsync()
    {
        if (Selected is null) return;
        await _db.Epreuves.DeleteAsync(Selected.Epreuve.Id);
        Selected = null;
        await LoadAsync();
    }

    private bool HasSelection() => Selected is not null;
}

public sealed class EpreuveDisplay(Epreuve epreuve, string classeNom)
{
    public Epreuve Epreuve    { get; } = epreuve;
    public string  Nom        => Epreuve.Nom;
    public string  ClasseNom  { get; } = classeNom;
    public int     Duree      => Epreuve.DureeMinutes;
    public string  Options    => BuildOptions();

    private string BuildOptions()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Epreuve.TiersTemps)    parts.Add("Tiers-temps");
        if (Epreuve.Ministerielle) parts.Add("Ministérielle");
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }
}
