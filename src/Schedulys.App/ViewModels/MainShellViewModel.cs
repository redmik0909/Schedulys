using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed record NavItem(string Label, string Section, PackIconKind Icon, bool IsHeader = false);

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
        new("PLANIFICATION",   "",            PackIconKind.None,          IsHeader: true),
        new("Horaire",         "horaire",     PackIconKind.CalendarMonth),
        new("DONNÉES",         "",            PackIconKind.None,          IsHeader: true),
        new("Enseignants",     "enseignants", PackIconKind.AccountTie),
        new("Groupes",         "groupes",     PackIconKind.AccountGroup),
        new("Épreuves",        "epreuves",    PackIconKind.ClipboardText),
        new("Locaux",          "locaux",      PackIconKind.DoorOpen),
        new("GESTION",         "",            PackIconKind.None,          IsHeader: true),
        new("Exporter",        "exporter",    PackIconKind.Download),
        new("Licence",         "licence",     PackIconKind.ShieldKey),
    };

    public LicenseInfo? License => App.License;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard))]
    [NotifyPropertyChangedFor(nameof(CurrentSectionLabel))]
    private NavItem? _selectedNavItem;

    public bool   IsDashboard        => SelectedNavItem?.Section is null or "dashboard";
    public bool   LicenceExpireBientot => App.License is { } l && l.ExpiresAt < DateTime.Now.AddDays(30);
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
        _ = MigrateQuotasParJourAsync();
        _ = MigrateProfsAsync();
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

    public string MachineId => LicenseService.GetMachineId();

    [RelayCommand]
    private void Renouveler()
    {
        var expiry = App.License?.ExpiresAt.ToString("yyyy-MM-dd") ?? "";
        var school = App.License?.SchoolName ?? "";
        var mailto = $"mailto:support@revolvittech.com?subject=Renouvellement%20Schedulys%20-%20{Uri.EscapeDataString(school)}&body=Bonjour%2C%0A%0AJe%20souhaite%20renouveler%20ma%20licence%20Schedulys.%0A%0A%C3%89cole%20%3A%20{Uri.EscapeDataString(school)}%0AExpiration%20actuelle%20%3A%20{expiry}%0A";
        Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ChangerCle()
    {
        var win = new Schedulys.App.Views.ActivationWindow();
        if (win.ShowDialog() == true && win.Result is { } info)
        {
            App.License = info;
            OnPropertyChanged(nameof(License));
            OnPropertyChanged(nameof(LicenceExpireBientot));
        }
    }

    private async Task MigrateQuotasParJourAsync()
    {
        if (!await DataSeeder.AreQuotasParJourSeededAsync(_db))
            await DataSeeder.SeedQuotasParJourAsync(_db);
    }

    private async Task MigrateProfsAsync()
    {
        if (!await DataSeeder.NeedsProfsResetAsync(_db)) return;
        await DataSeeder.ResetProfsAsync(_db);
        await Task.WhenAll(LoadDashboardAsync(), Teachers.LoadAsync());
    }

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
