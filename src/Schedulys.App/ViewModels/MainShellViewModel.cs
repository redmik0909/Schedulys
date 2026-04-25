using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed record NavItem(string Label, string Section, PackIconKind Icon);

public sealed partial class MainShellViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public TeachersViewModel  Teachers  { get; }
    public LocationsViewModel Locations { get; }
    public GroupesViewModel   Groupes   { get; }
    public ExamsViewModel     Exams     { get; }
    public PlanningViewModel  Planning  { get; }
    public ExportsViewModel   Exports   { get; }

    public IReadOnlyList<NavItem> NavItems { get; } = new NavItem[]
    {
        new("Tableau de bord", "dashboard",   PackIconKind.ViewDashboard),
        new("Horaire",         "horaire",     PackIconKind.CalendarMonth),
        new("Enseignants",     "enseignants", PackIconKind.AccountTie),
        new("Groupes",         "groupes",     PackIconKind.AccountGroup),
        new("Épreuves",        "epreuves",    PackIconKind.ClipboardText),
        new("Locaux",          "locaux",      PackIconKind.DoorOpen),
        new("Exporter",        "exporter",    PackIconKind.Download),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard))]
    [NotifyPropertyChangedFor(nameof(CurrentSectionLabel))]
    private NavItem? _selectedNavItem;

    public bool   IsDashboard        => SelectedNavItem?.Section is null or "dashboard";
    public string CurrentSectionLabel => SelectedNavItem?.Label ?? "Tableau de bord";

    [ObservableProperty] private int    _statEnseignants;
    [ObservableProperty] private int    _statGroupes;
    [ObservableProperty] private int    _statLocaux;
    [ObservableProperty] private int    _statEpreuves;
    [ObservableProperty] private int    _statCreneaux;
    [ObservableProperty] private string _importStatus = "";
    [ObservableProperty] private bool   _importEnCours;

    public string TodayLabel
    {
        get
        {
            var s = DateTime.Now.ToString("dddd dd MMMM yyyy", new CultureInfo("fr-CA"));
            return char.ToUpper(s[0]) + s[1..];
        }
    }

    public MainShellViewModel(DataContext db)
    {
        _db       = db;
        Teachers  = new TeachersViewModel(db);
        Locations = new LocationsViewModel(db);
        Groupes   = new GroupesViewModel(db);
        Exams     = new ExamsViewModel(db);
        Planning  = new PlanningViewModel(db);
        Exports   = new ExportsViewModel(db);

        _selectedNavItem = NavItems[0];
        _ = LoadDashboardAsync();
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value?.Section is null or "dashboard")
            _ = LoadDashboardAsync();
    }

    [RelayCommand(CanExecute = nameof(PeutImporter))]
    private async Task ImporterDonneesAsync()
    {
        ImportEnCours = true;
        ImporterDonneesCommand.NotifyCanExecuteChanged();
        ImportStatus  = "Vérification...";
        try
        {
            if (await DataSeeder.IsAlreadySeededAsync(_db))
            {
                ImportStatus = "Données déjà importées.";
                return;
            }
            ImportStatus = "Import en cours...";
            await DataSeeder.SeedAsync(_db);
            await Task.WhenAll(
                LoadDashboardAsync(),
                Teachers.LoadAsync(),
                Locations.LoadAsync(),
                Groupes.LoadAsync(),
                Exams.RefreshAsync(),
                Planning.ReloadAllAsync()
            );
            ImportStatus = "Import terminé avec succès !";
        }
        catch (Exception ex)
        {
            ImportStatus = $"Erreur : {ex.Message}";
        }
        finally
        {
            ImportEnCours = false;
            ImporterDonneesCommand.NotifyCanExecuteChanged();
        }
    }

    private bool PeutImporter() => !ImportEnCours;

    private async Task LoadDashboardAsync()
    {
        StatEnseignants = (await _db.Profs.ListAsync()).Count;
        StatGroupes     = (await _db.Classes.ListAsync()).Count;
        StatLocaux      = (await _db.Salles.ListAsync()).Count;
        StatEpreuves    = (await _db.Epreuves.ListAsync()).Count;

        // Nombre de sessions cette semaine (lundi → vendredi)
        var today = DateOnly.FromDateTime(DateTime.Today);
        int dow   = (int)today.DayOfWeek;
        var lundi = today.AddDays(dow == 0 ? -6 : 1 - dow);
        var ven   = lundi.AddDays(4);
        StatCreneaux = (await _db.Sessions.ListByPeriodeAsync(lundi, ven)).Count;
    }
}
