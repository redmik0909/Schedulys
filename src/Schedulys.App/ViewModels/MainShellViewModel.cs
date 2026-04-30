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

    public TeachersViewModel      Teachers      { get; }
    public LocationsViewModel     Locations     { get; }
    public GroupesViewModel       Groupes       { get; }
    public ExamsViewModel         Exams         { get; }
    public PlanningViewModel      Planning      { get; }
    public SurveillanceViewModel  Surveillance  { get; }
    public ExportsViewModel       Exports       { get; }
    public ParametresViewModel    Parametres    { get; }

    public IReadOnlyList<NavItem> NavItems { get; } = new NavItem[]
    {
        new("Tableau de bord", "dashboard",   PackIconKind.ViewDashboard),
        new("PLANIFICATION",   "",            PackIconKind.None,          IsHeader: true),
        new("Horaire",         "horaire",     PackIconKind.CalendarMonth),
        new("DONNÉES",         "",            PackIconKind.None,          IsHeader: true),
        new("Personnel",       "enseignants", PackIconKind.AccountTie),
        new("Groupes",         "groupes",     PackIconKind.AccountGroup),
        new("Épreuves",        "epreuves",    PackIconKind.ClipboardText),
        new("Surveillance",    "surveillance",PackIconKind.ShieldAccount),
        new("Locaux",          "locaux",      PackIconKind.DoorOpen),
        new("GESTION",         "",            PackIconKind.None,          IsHeader: true),
        new("Exporter",        "exporter",    PackIconKind.Download),
        new("Paramètres",      "parametres",  PackIconKind.Cog),
        new("Licence",         "licence",     PackIconKind.ShieldKey),
    };

    public LicenseInfo? License => App.License;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDashboard))]
    [NotifyPropertyChangedFor(nameof(CurrentSectionLabel))]
    private NavItem? _selectedNavItem;

    public bool   IsDashboard          => SelectedNavItem?.Section is null or "dashboard";
    public bool   LicenceExpireBientot => App.License is { } l && !l.IsTrial && l.ExpiresAt < DateTime.UtcNow.AddDays(30);
    public bool   IsTrialLicense       => App.License?.IsTrial == true;
    public int    TrialJoursRestants   => App.License is { } l ? Math.Max(0, (int)(l.ExpiresAt - DateTime.UtcNow).TotalDays) : 0;
    public string CurrentSectionLabel  => SelectedNavItem?.Label ?? "Tableau de bord";

    [ObservableProperty] private int    _statEnseignants;
    [ObservableProperty] private int    _statGroupes;
    [ObservableProperty] private int    _statLocaux;
    [ObservableProperty] private int    _statEpreuves;
    [ObservableProperty] private int    _statCreneaux;

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
        Teachers     = new TeachersViewModel(db);
        Locations    = new LocationsViewModel(db);
        Groupes      = new GroupesViewModel(db);
        Exams        = new ExamsViewModel(db);
        Planning     = new PlanningViewModel(db);
        Surveillance = new SurveillanceViewModel(db);
        Exports      = new ExportsViewModel(db);
        Parametres   = new ParametresViewModel(db);

        _selectedNavItem = NavItems[0];
        _ = LoadDashboardAsync();
        _ = MigrateAsync();
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        if (value?.Section is null or "dashboard")
            _ = LoadDashboardAsync();
        else if (value.Section == "surveillance")
            _ = Surveillance.RefreshAsync();
    }

    public string MachineId   => LicenseService.GetMachineId();
    public string AppVersion  => $"v{UpdateChecker.CurrentVersion}";

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

    [RelayCommand]
    private async Task DiagnosticAsync()
    {
        try
        {
            var profs     = (await _db.Profs.ListAsync()).Count;
            var classes   = (await _db.Classes.ListAsync()).Count;
            var salles    = (await _db.Salles.ListAsync()).Count;
            var epreuves  = (await _db.Epreuves.ListAsync()).Count;
            var sessions  = (await _db.Sessions.ListByPeriodeAsync(
                                DateOnly.FromDateTime(DateTime.Today.AddYears(-2)),
                                DateOnly.FromDateTime(DateTime.Today.AddYears(2)))).Count;

            var dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Schedulys", "data.db");
            var dbSize = System.IO.File.Exists(dbPath)
                ? $"{new System.IO.FileInfo(dbPath).Length / 1024.0:F1} KB"
                : "introuvable";

            System.Windows.MessageBox.Show(
                $"Base de données : {dbPath}\nTaille : {dbSize}\n\n" +
                $"Enseignants : {profs}\n" +
                $"Groupes (Classes) : {classes}\n" +
                $"Salles : {salles}\n" +
                $"Épreuves : {epreuves}\n" +
                $"Sessions : {sessions}\n\n" +
                $"Version : {UpdateChecker.CurrentVersion}",
                "Diagnostic — Schedulys",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Erreur lors du diagnostic :\n{ex.Message}",
                "Diagnostic — Erreur",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task MigrateAsync()
    {
        if (await DataSeeder.NeedsProfsResetAsync(_db))
        {
            AppLogger.Warn("MIGRATE", "Enseignants legacy détectés — réinitialisation.");
            await DataSeeder.ResetProfsAsync(_db, msg => AppLogger.Info("MIGRATE", msg));
            await Task.WhenAll(LoadDashboardAsync(), Teachers.LoadAsync());
        }
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
