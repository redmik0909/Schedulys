using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Core.Services;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

// ─────────────────────────────────────────────────────────────────────────────
//  Display helpers
// ─────────────────────────────────────────────────────────────────────────────

public sealed class GroupeExamenDisplay(
    GroupeExamen groupe, string description, string enseignant, string surveillant, string salle, string heureDebut)
{
    public GroupeExamen Groupe      { get; } = groupe;
    public string       Description { get; } = description;
    public string       Enseignant  { get; } = enseignant;
    public string       Surveillant { get; } = surveillant;
    public string       Salle       { get; } = salle;
    public string       HeureDebut  { get; } = heureDebut;

    public string ElèvesLabel  => $"{Groupe.NbEleves} élèves";
    public string PlageHoraire => string.IsNullOrWhiteSpace(Groupe.HeureFin)
        ? HeureDebut
        : $"{HeureDebut}–{Groupe.HeureFin}";
    public string DepartLabel  => string.IsNullOrWhiteSpace(Groupe.PremierDepart)
        ? ""
        : $"  ·  1er départ {Groupe.PremierDepart}";

    public string Ligne1 => $"{Description}  {Groupe.CodeGroupe}".Trim();
    public string Ligne2 => $"{Salle}  ·  {Surveillant}  ·  {ElèvesLabel}  ·  {PlageHoraire}{DepartLabel}".Trim(' ', '·');
}

public sealed class RoleSurveillanceDisplay(RoleSurveillance role, string surveillant)
{
    public RoleSurveillance Role        { get; } = role;
    public string           Surveillant { get; } = surveillant;

    public string Ligne1 => Role.TypeRole;
    public string Ligne2 => string.IsNullOrWhiteSpace(Role.Local)
        ? $"{Surveillant}  ·  {Role.DureeMinutes} min"
        : $"{Surveillant}  ·  Local {Role.Local}  ·  {Role.DureeMinutes} min";
}

public sealed partial class GroupeFormItem : ObservableObject
{
    public Classe                    Classe                  { get; }
    public ObservableCollection<Salle> SallesDisponibles     { get; } = new();
    public IReadOnlyList<Prof>       SurveillantsDisponibles { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvertissementSalle))]
    private Salle? _salleSelectionnee;

    [ObservableProperty] private Prof? _surveillantSelectionne;

    public string Label => string.IsNullOrWhiteSpace(Classe.Description)
        ? $"{Classe.Code}  ·  {Classe.Effectif} élèves"
        : $"{Classe.Code} — {Classe.Description}  ·  {Classe.Effectif} élèves";

    public string AvertissementSalle
    {
        get
        {
            if (SalleSelectionnee is null || Classe.Effectif <= 0) return "";
            return SalleSelectionnee.Capacite < Classe.Effectif
                ? $"⚠ {SalleSelectionnee.Nom} : {SalleSelectionnee.Capacite} pl. / {Classe.Effectif} élèves"
                : "";
        }
    }

    public GroupeFormItem(Classe classe, IEnumerable<Salle> salles, IReadOnlyList<Prof> profs)
    {
        Classe = classe;
        foreach (var s in salles) SallesDisponibles.Add(s);
        SurveillantsDisponibles = profs;
    }
}

public sealed partial class SessionDisplay : ObservableObject
{
    public SessionDisplay(Session session) => Session = session;

    public Session                                     Session { get; }
    public ObservableCollection<GroupeExamenDisplay>   Groupes { get; } = new();
    public ObservableCollection<RoleSurveillanceDisplay> Roles { get; } = new();

    [ObservableProperty] private bool _isSelected;

    public string DateLabel
    {
        get
        {
            if (!DateOnly.TryParse(Session.Date, out var d)) return Session.Date;
            var raw = d.ToString("dddd d MMMM yyyy", new CultureInfo("fr-CA"));
            return char.ToUpper(raw[0]) + raw[1..];
        }
    }
    public string PeriodeLabel  => Session.Periode == "AM" ? "Matin" : "Après-midi";
    public string HeureLabel    => Session.HeureDebut;
    public string EnTete        => Session.JourCycle > 0
        ? $"{DateLabel}  —  {PeriodeLabel}  ({HeureLabel})  —  Jour {Session.JourCycle}"
        : $"{DateLabel}  —  {PeriodeLabel}  ({HeureLabel})";
    public int    TotalGroupes  => Groupes.Count;
    public int    TotalMinutes  => Groupes.Sum(g => g.Groupe.DureeMinutes)
                                 + Roles.Sum(r => r.Role.DureeMinutes);
}

public sealed partial class ProfMinutesDisplay : ObservableObject
{
    public Prof   Prof           { get; init; } = null!;
    [ObservableProperty] private int  _minutesAssignees;
    [ObservableProperty] private int  _minutesMax;

    public int    Pourcentage    => MinutesMax > 0 ? Math.Min(100, (int)(((long)MinutesAssignees * 100) / MinutesMax)) : 0;
    public bool   IsOverQuota    => MinutesMax > 0 && MinutesAssignees > MinutesMax;
    public string Label          => $"{Prof.Nom}  {MinutesAssignees}/{(MinutesMax > 0 ? MinutesMax.ToString() : "—")} min";
    public string BarColor       => IsOverQuota ? "#EF5350" : (Pourcentage >= 80 ? "#FF9800" : "#4CAF50");

    partial void OnMinutesAssigneesChanged(int value)
    {
        OnPropertyChanged(nameof(Pourcentage));
        OnPropertyChanged(nameof(IsOverQuota));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(BarColor));
    }
    partial void OnMinutesMaxChanged(int value)
    {
        OnPropertyChanged(nameof(Pourcentage));
        OnPropertyChanged(nameof(IsOverQuota));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(BarColor));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  Semaine display
// ─────────────────────────────────────────────────────────────────────────────

public sealed class JourSemaineDisplay
{
    public DateOnly Date { get; init; }
    public string NomJour { get; init; } = "";
    public string DateLabel => Date.ToString("d MMM", new System.Globalization.CultureInfo("fr-CA"));
    public int JourCycle { get; set; }
    public string JourCycleLabel => JourCycle > 0 ? $"Jour {JourCycle}" : "";
    public bool EstAujourdHui => Date == DateOnly.FromDateTime(DateTime.Today);
    public ObservableCollection<SessionDisplay> Sessions { get; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────
//  ViewModel principal
// ─────────────────────────────────────────────────────────────────────────────

public sealed partial class PlanningViewModel : ViewModelBase
{
    private readonly DataContext      _db;
    private readonly PlanningSettings _settings = new();

    // Cache des données de référence — chargées une seule fois, réutilisées sans re-fetch
    private IReadOnlyList<Prof>                  _profsCache    = Array.Empty<Prof>();
    private IReadOnlyList<Salle>                 _sallesCache   = Array.Empty<Salle>();
    private IReadOnlyList<Core.Models.Epreuve>   _epreuvesCache = Array.Empty<Core.Models.Epreuve>();
    private IReadOnlyList<Classe>                _classesCache  = Array.Empty<Classe>();

    // Vue semaine
    public ObservableCollection<JourSemaineDisplay> JoursDeSemaine { get; } = new();

    // Listes principales (conservées pour compatibilité)
    public ObservableCollection<SessionDisplay>      Sessions          { get; } = new();
    public ObservableCollection<ProfMinutesDisplay>  MinutesParProf    { get; } = new();

    // Listes pour les formulaires
    public ObservableCollection<Classe>               ClassesDisponibles  { get; } = new();
    public ObservableCollection<Core.Models.Epreuve>  EpreuvesDisponibles { get; } = new();
    public ObservableCollection<Classe>               ClassesGroupe       { get; } = new();
    public ObservableCollection<GroupeFormItem>       GroupeFormItems     { get; } = new();
    public ObservableCollection<Prof>                 ProfsDisponibles    { get; } = new();
    public ObservableCollection<Salle>                SallesDisponibles   { get; } = new();

    // ── Formulaire: Nouvelle session ──────────────────────────────────────────
    [ObservableProperty] private DateTime _dateNouvelleSession = NearestWeekday(DateTime.Today);
    [ObservableProperty] private string   _periodeNouvelleSession = "AM";
    [ObservableProperty] private string   _heureDebutNouvelleSession = "08:30";

    public record JourCycleItem(int Valeur, string Libelle);
    public IReadOnlyList<JourCycleItem> JoursDuCycle { get; } =
        Enumerable.Range(0, 10)
            .Select(j => new JourCycleItem(j, j == 0 ? "Non défini" : $"Jour {j}"))
            .ToList();
    [ObservableProperty] private JourCycleItem _jourCycleSession = null!;

    public IReadOnlyList<string> Periodes { get; } = new[] { "AM", "PM" };

    // ── Formulaire: Nouveau groupe dans la session sélectionnée ──────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSessionSelectionnee))]
    private SessionDisplay? _sessionSelectionnee;

    public bool HasSessionSelectionnee => SessionSelectionnee is not null;

    partial void OnSessionSelectionneeChanged(SessionDisplay? value)
    {
        foreach (var s in Sessions) s.IsSelected = (s == value);
        if (value is not null) OngletActif = 1;
    }

    [ObservableProperty] private int _ongletActif = 0;

    [ObservableProperty] private Core.Models.Epreuve? _epreuveSelectionnee;
    [ObservableProperty] private Prof?              _surveillantSelectionne;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvertissementSalle))]
    private Salle? _salleSelectionnee;
    [ObservableProperty] private string  _heureFinInput      = "11:30";
    [ObservableProperty] private string  _premierDepartInput = "10:30";
    [ObservableProperty] private bool    _tiersTemps;
    [ObservableProperty] private string  _typeGroupe         = "Standard";

    public IReadOnlyList<string> TypesGroupe { get; } = new[] { "Standard", "SAI", "EHDAA" };

    public string AvertissementSalle
    {
        get
        {
            if (SalleSelectionnee is null) return "";
            var total = ClassesGroupe.Sum(c => c.Effectif);
            if (total <= 0) return "";
            if (SalleSelectionnee.Capacite < total)
                return $"⚠ La salle {SalleSelectionnee.Nom} n'a que {SalleSelectionnee.Capacite} place(s) pour {total} élève(s).";
            return "";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuotaDepasseVisible))]
    private string _quotaDepasseMessage = "";
    public bool QuotaDepasseVisible => !string.IsNullOrEmpty(QuotaDepasseMessage);

    // ── Formulaire: Nouveau rôle de surveillance ─────────────────────────────
    [ObservableProperty] private string  _typeRoleInput       = "Surveillance 1er étage";
    [ObservableProperty] private Prof?   _surveillantRoleSelectionne;
    [ObservableProperty] private string  _localRoleInput      = "";
    [ObservableProperty] private string  _dureeRoleInput      = "90";

    public IReadOnlyList<string> TypesRole { get; } = new[]
    {
        "Surveillance 1er étage", "Surveillance 3e étage",
        "Surveillance bibliothèque SAI", "Disponibilités et pauses"
    };

    [ObservableProperty] private string _messageErreur = "";

    // ── Navigation semaine ────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemaineLabel))]
    private DateOnly _semaineDebut = GetLundiDeSemaine(DateOnly.FromDateTime(DateTime.Today));

    public string SemaineLabel
    {
        get
        {
            var fin = SemaineDebut.AddDays(4);
            var culture = new System.Globalization.CultureInfo("fr-CA");
            if (SemaineDebut.Month == fin.Month)
                return $"{SemaineDebut.Day}–{fin.Day} {fin.ToString("MMMM yyyy", culture)}";
            return $"{SemaineDebut.ToString("d MMM", culture)} – {fin.ToString("d MMM yyyy", culture)}";
        }
    }

    public PlanningViewModel(DataContext db)
    {
        _db = db;
        _jourCycleSession = JoursDuCycle[0];
        _ = LoadAsync();
    }

    private static DateOnly GetLundiDeSemaine(DateOnly d)
    {
        int dow = (int)d.DayOfWeek;
        return d.AddDays(dow == 0 ? -6 : 1 - dow);
    }

    private static DateTime NearestWeekday(DateTime d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(-1),
        DayOfWeek.Sunday   => d.AddDays(-2),
        _                  => d
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  Chargement
    // ─────────────────────────────────────────────────────────────────────────

    partial void OnEpreuveSelectionneeChanged(Core.Models.Epreuve? value)
    {
        ClassesGroupe.Clear();
        ClearGroupeFormItems();
        if (value is null) return;
        TiersTemps = value.TiersTemps;
        MettreAJourHeureFin();
        _ = LoadClassesGroupeAsync(value);
    }

    private void ClearGroupeFormItems()
    {
        foreach (var item in GroupeFormItems)
            item.PropertyChanged -= OnGroupeItemSalleChanged;
        GroupeFormItems.Clear();
    }

    private void OnGroupeItemSalleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupeFormItem.SalleSelectionnee))
            ActualiserSallesDisponibles();
    }

    private void ActualiserSallesDisponibles()
    {
        var prises = GroupeFormItems
            .Where(i => i.SalleSelectionnee is not null)
            .Select(i => i.SalleSelectionnee!.Id)
            .ToHashSet();

        foreach (var item in GroupeFormItems)
        {
            var selection = item.SalleSelectionnee;
            item.SallesDisponibles.Clear();
            foreach (var s in _sallesCache)
            {
                if (!prises.Contains(s.Id) || s.Id == selection?.Id)
                    item.SallesDisponibles.Add(s);
            }
        }
    }

    private async Task LoadClassesGroupeAsync(Core.Models.Epreuve epreuve)
    {
        var classeIds = await _db.Epreuves.GetGroupeIdsAsync(epreuve.Id);
        var idSet     = classeIds.ToHashSet();
        var salles    = _sallesCache.Count > 0 ? _sallesCache : await _db.Salles.ListAsync();
        var profs     = _profsCache.Count  > 0 ? _profsCache  : await _db.Profs.ListAsync();

        ClassesGroupe.Clear();
        ClearGroupeFormItems();
        foreach (var c in ClassesDisponibles.Where(c => idSet.Contains(c.Id)).OrderBy(c => c.Code))
        {
            ClassesGroupe.Add(c);
            var item = new GroupeFormItem(c, salles, profs);
            item.PropertyChanged += OnGroupeItemSalleChanged;
            GroupeFormItems.Add(item);
        }
    }

    partial void OnTiersTempsChanged(bool value) => MettreAJourHeureFin();

    private void MettreAJourHeureFin()
    {
        if (EpreuveSelectionnee is null || SessionSelectionnee is null) return;
        if (!TimeSpan.TryParse(SessionSelectionnee.Session.HeureDebut, out var debut)) return;
        var duree = CalculerDureeEffective(EpreuveSelectionnee.DureeMinutes, TiersTemps);
        var fin   = debut.Add(TimeSpan.FromMinutes(duree));
        HeureFinInput = $"{fin.Hours:D2}:{fin.Minutes:D2}";
    }

    private static int CalculerDureeEffective(int dureeBase, bool tiersTemps)
        => tiersTemps ? (int)Math.Ceiling(dureeBase * 4.0 / 3) : dureeBase;

    private async Task LoadAsync()
    {
        _classesCache  = await _db.Classes.ListAsync();
        _profsCache    = await _db.Profs.ListAsync();
        _sallesCache   = await _db.Salles.ListAsync();
        _epreuvesCache = await _db.Epreuves.ListAsync(annee: AppConstants.AnneeScolaire);

        ClassesDisponibles.Clear();
        foreach (var c in _classesCache.OrderBy(c => c.Code)) ClassesDisponibles.Add(c);

        EpreuvesDisponibles.Clear();
        foreach (var e in _epreuvesCache) EpreuvesDisponibles.Add(e);

        ProfsDisponibles.Clear();
        foreach (var p in _profsCache) ProfsDisponibles.Add(p);

        SallesDisponibles.Clear();
        foreach (var s in _sallesCache) SallesDisponibles.Add(s);

        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var profs    = _profsCache.Count    > 0 ? _profsCache    : await _db.Profs.ListAsync();
        var salles   = _sallesCache.Count   > 0 ? _sallesCache   : await _db.Salles.ListAsync();
        var epreuves = _epreuvesCache.Count > 0 ? _epreuvesCache : await _db.Epreuves.ListAsync();
        var classes  = _classesCache.Count  > 0 ? _classesCache  : await _db.Classes.ListAsync();

        var savedId  = SessionSelectionnee?.Session.Id;
        Sessions.Clear();
        JoursDeSemaine.Clear();

        var joursNoms = new[] { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi" };
        for (int i = 0; i < 5; i++)
        {
            var date = SemaineDebut.AddDays(i);
            JoursDeSemaine.Add(new JourSemaineDisplay
            {
                Date    = date,
                NomJour = joursNoms[i]
            });
        }

        var debut = SemaineDebut;
        var fin   = SemaineDebut.AddDays(4);
        var allSessions = await _db.Sessions.ListByPeriodeAsync(debut, fin);

        foreach (var s in allSessions)
        {
            var sd = await BuildSessionDisplayAsync(s, profs, salles, epreuves, classes);

            var jour = JoursDeSemaine.FirstOrDefault(j =>
                j.Date.ToString("yyyy-MM-dd") == s.Date);
            jour?.Sessions.Add(sd);

            if (jour is not null && s.JourCycle > 0)
                jour.JourCycle = s.JourCycle;

            Sessions.Add(sd);
        }

        if (savedId.HasValue)
            SessionSelectionnee = Sessions.FirstOrDefault(x => x.Session.Id == savedId);
    }

    private async Task<SessionDisplay> BuildSessionDisplayAsync(
        Session s,
        IReadOnlyList<Prof>   profs,
        IReadOnlyList<Salle>  salles,
        IReadOnlyList<Core.Models.Epreuve> epreuves,
        IReadOnlyList<Classe> classes)
    {
        var sd      = new SessionDisplay(s);
        var groupes = await _db.GroupesExamen.ListBySessionAsync(s.Id);

        foreach (var g in groupes)
        {
            string description;
            if (g.ClasseId.HasValue)
            {
                var cl = classes.FirstOrDefault(c => c.Id == g.ClasseId);
                description = cl is not null
                    ? (string.IsNullOrWhiteSpace(cl.Description) ? cl.Code : cl.Description)
                    : g.CodeGroupe;
            }
            else
            {
                var ep = epreuves.FirstOrDefault(x => x.Id == g.EpreuveId);
                description = ep?.Nom ?? g.CodeGroupe;
            }

            var ens = profs.FirstOrDefault(p => p.Id == g.EnseignantId);
            var sur = g.SurveillantId.HasValue ? profs.FirstOrDefault(p => p.Id == g.SurveillantId) : null;
            var sal = g.SalleId.HasValue      ? salles.FirstOrDefault(x => x.Id == g.SalleId)       : null;

            sd.Groupes.Add(new GroupeExamenDisplay(g, description,
                ens?.Nom ?? "—", sur?.Nom ?? "—", sal?.Nom ?? "—", s.HeureDebut));
        }

        var roles = await _db.RolesSurveillance.ListBySessionAsync(s.Id);
        foreach (var r in roles)
        {
            var sur = profs.FirstOrDefault(p => p.Id == r.SurveillantId);
            sd.Roles.Add(new RoleSurveillanceDisplay(r, sur?.Nom ?? "—"));
        }

        return sd;
    }

    private async Task LoadMinutesAsync()
    {
        var date     = DateOnly.FromDateTime(DateTime.Today);
        var profs    = _profsCache.Count > 0 ? _profsCache : await _db.Profs.ListAsync();
        var quotas   = await _db.Quotas.ListAsync();
        var sessions = await _db.Sessions.ListByDateAsync(date);
        var minutesByProf = await _db.GroupesExamen.GetMinutesAssigneesByProfAsync(date);

        // Déterminer le jour de cycle de la journée (premier trouvé non nul)
        var jourCycle = sessions.FirstOrDefault(s => s.JourCycle > 0)?.JourCycle ?? 0;

        MinutesParProf.Clear();
        foreach (var p in profs)
        {
            // Quota spécifique au jour de cycle, sinon repli sur la moyenne (JourCycle=0)
            var quota = (jourCycle > 0
                ? quotas.FirstOrDefault(q => q.ProfId == p.Id && q.JourCycle == jourCycle)
                : null)
                ?? quotas.FirstOrDefault(q => q.ProfId == p.Id && q.JourCycle == 0);
            MinutesParProf.Add(new ProfMinutesDisplay
            {
                Prof             = p,
                MinutesAssignees = minutesByProf.TryGetValue(p.Id, out var m) ? m : 0,
                MinutesMax       = quota?.MinutesMax ?? 0
            });
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    public async Task ReloadAllAsync() => await LoadAsync();

    // ─────────────────────────────────────────────────────────────────────────
    //  Navigation de date
    // ─────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SemainePrecedenteAsync()
    {
        SemaineDebut = SemaineDebut.AddDays(-7);
        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    [RelayCommand]
    private async Task SemaineSuivanteAsync()
    {
        SemaineDebut = SemaineDebut.AddDays(7);
        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    [RelayCommand]
    private async Task CetteSemaineAsync()
    {
        SemaineDebut = GetLundiDeSemaine(DateOnly.FromDateTime(DateTime.Today));
        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Créer une session
    // ─────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreerSessionAsync()
    {
        MessageErreur = "";

        if (DateNouvelleSession.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            MessageErreur = "La vue semaine affiche lun–ven. Veuillez choisir un jour de semaine.";
            return;
        }

        if (!TimeSpan.TryParseExact(HeureDebutNouvelleSession, @"hh\:mm", CultureInfo.InvariantCulture, out _))
        {
            MessageErreur = "Format d'heure invalide (HH:mm).";
            return;
        }

        var session = new Session
        {
            Date          = DateNouvelleSession.ToString("yyyy-MM-dd"),
            Periode       = PeriodeNouvelleSession,
            HeureDebut    = HeureDebutNouvelleSession,
            JourCycle     = JourCycleSession?.Valeur ?? 0,
            AnneeScolaire = AppConstants.AnneeScolaire
        };

        await _db.Sessions.CreateAsync(session);
        AppLogger.Info("DATA", $"Session créée : Date={session.Date} Période={session.Periode} JourCycle={session.JourCycle}");

        // Naviguer vers la semaine de la session créée
        SemaineDebut = GetLundiDeSemaine(DateOnly.FromDateTime(DateNouvelleSession));
        await LoadSessionsAsync();
        await LoadMinutesAsync();

        // Sélectionner la nouvelle session
        SessionSelectionnee = Sessions.LastOrDefault();
    }

    [RelayCommand]
    private async Task SupprimerSessionAsync(SessionDisplay? sd)
    {
        if (sd is null) return;
        AppLogger.Warn("DATA", $"Suppression session Id={sd.Session.Id} Date={sd.Session.Date} Période={sd.Session.Periode}");
        await _db.Sessions.DeleteAsync(sd.Session.Id);
        if (SessionSelectionnee?.Session.Id == sd.Session.Id)
            SessionSelectionnee = null;
        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ajouter un groupe à la session sélectionnée
    // ─────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task AjouterGroupeAsync() => AjouterGroupeInternalAsync(forcerQuota: false);

    [RelayCommand]
    private Task ForcerAjoutAsync() => AjouterGroupeInternalAsync(forcerQuota: true);

    private async Task AjouterGroupeInternalAsync(bool forcerQuota)
    {
        MessageErreur       = "";
        QuotaDepasseMessage = "";

        try
        {

        if (SessionSelectionnee is null)   { MessageErreur = "Sélectionnez une session.";         return; }
        if (EpreuveSelectionnee is null)   { MessageErreur = "Sélectionnez une épreuve.";         return; }
        if (GroupeFormItems.Count == 0)    { MessageErreur = "Aucun groupe lié à cette épreuve."; return; }

        if (!TimeSpan.TryParse(HeureFinInput, out var heureFinTs))
                                           { MessageErreur = "Heure de fin invalide (HH:mm).";    return; }
        if (!TimeSpan.TryParse(SessionSelectionnee.Session.HeureDebut, out var heureDebutTs))
                                           { MessageErreur = "Heure de début de la session invalide."; return; }
        var dureeMinutes = (int)(heureFinTs - heureDebutTs).TotalMinutes;
        if (dureeMinutes <= 0)             { MessageErreur = "L'heure de fin doit être après l'heure de début."; return; }

        // Vérifier qu'aucun surveillant n'est assigné à deux groupes en même temps
        var survIds = GroupeFormItems
            .Where(i => i.SurveillantSelectionne is not null)
            .Select(i => i.SurveillantSelectionne!.Id)
            .ToList();
        var doublons = survIds.GroupBy(id => id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        if (doublons.Count > 0)
        {
            var noms = GroupeFormItems
                .Where(i => i.SurveillantSelectionne is not null && doublons.Contains(i.SurveillantSelectionne.Id))
                .Select(i => i.SurveillantSelectionne!.Nom).Distinct();
            MessageErreur = $"Un surveillant ne peut couvrir qu'un seul groupe : {string.Join(", ", noms)}.";
            return;
        }

        var groupesExistants = await _db.GroupesExamen.ListBySessionAsync(SessionSelectionnee.Session.Id);
        var rolesExistants   = await _db.RolesSurveillance.ListBySessionAsync(SessionSelectionnee.Session.Id);

        foreach (var item in GroupeFormItems.Where(i => i.SurveillantSelectionne is not null))
        {
            var surv = item.SurveillantSelectionne!;
            if (groupesExistants.Any(g => g.SurveillantId == surv.Id))
            {
                MessageErreur = $"{surv.Nom} surveille déjà un groupe dans cette session.";
                return;
            }
            if (rolesExistants.Any(r => r.SurveillantId == surv.Id))
            {
                MessageErreur = $"{surv.Nom} a déjà un rôle de surveillance dans cette session.";
                return;
            }

            if (!forcerQuota && DateOnly.TryParse(SessionSelectionnee.Session.Date, out var dateSurv))
            {
                var jourCycle = SessionSelectionnee.Session.JourCycle;
                var quota = jourCycle > 0
                    ? await _db.Quotas.GetByProfAsync(surv.Id, jourCycle, AppConstants.AnneeScolaire)
                    : null;
                quota ??= await _db.Quotas.GetByProfAsync(surv.Id, 0, AppConstants.AnneeScolaire);

                if (quota?.MinutesMax > 0)
                {
                    var minutesDejaAssignees = await _db.GroupesExamen.GetMinutesAssigneesAsync(surv.Id, dateSurv);
                    var total = minutesDejaAssignees + dureeMinutes;
                    if (total > quota.MinutesMax)
                    {
                        QuotaDepasseMessage = $"⚠ {surv.Nom} dépasserait son quota : " +
                                             $"{total} min assignées / {quota.MinutesMax} min autorisées ce jour.";
                        return;
                    }
                }
            }
        }

        try
        {
            foreach (var item in GroupeFormItems)
            {
                var groupe = new GroupeExamen
                {
                    SessionId     = SessionSelectionnee.Session.Id,
                    EpreuveId     = EpreuveSelectionnee.Id,
                    ClasseId      = item.Classe.Id,
                    CodeGroupe    = item.Classe.Code,
                    EnseignantId  = item.Classe.ProfId,
                    NbEleves      = item.Classe.Effectif,
                    SurveillantId = item.SurveillantSelectionne?.Id,
                    SalleId       = item.SalleSelectionnee?.Id,
                    TiersTemps    = TiersTemps,
                    DureeMinutes  = dureeMinutes,
                    Type          = "Standard",
                    HeureFin      = HeureFinInput.Trim(),
                    PremierDepart = PremierDepartInput.Trim()
                };
                await _db.GroupesExamen.CreateAsync(groupe);
                AppLogger.Info("DATA", $"GroupeExamen créé : Épreuve='{EpreuveSelectionnee.Nom}' Classe={item.Classe.Code} Salle={item.SalleSelectionnee?.Nom ?? "—"} Surv={item.SurveillantSelectionne?.Nom ?? "—"}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("DATA", "Erreur création GroupeExamen", ex);
            MessageErreur = $"Erreur lors de l'ajout : {ex.Message}";
            return;
        }

        QuotaDepasseMessage = "";
        EpreuveSelectionnee = null;
        ClearGroupeFormItems();
        ClassesGroupe.Clear();
        HeureFinInput       = "11:30";
        PremierDepartInput  = "10:30";

        await LoadSessionsAsync();
        await LoadMinutesAsync();

        } catch (Exception ex) { MessageErreur = $"Erreur : {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SupprimerGroupeAsync(GroupeExamenDisplay? gd)
    {
        if (gd is null) return;
        AppLogger.Warn("DATA", $"Suppression GroupeExamen Id={gd.Groupe.Id} Code={gd.Groupe.CodeGroupe}");
        await _db.GroupesExamen.DeleteAsync(gd.Groupe.Id);
        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Ajouter un rôle de surveillance à la session sélectionnée
    // ─────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AjouterRoleAsync()
    {
        MessageErreur = "";

        if (SessionSelectionnee is null)            { MessageErreur = "Sélectionnez une session."; return; }
        if (SurveillantRoleSelectionne is null)     { MessageErreur = "Sélectionnez un surveillant."; return; }
        if (!int.TryParse(DureeRoleInput, out var duree) || duree <= 0)
                                                     { MessageErreur = "Durée invalide (en minutes)."; return; }

        // Vérifier conflit dans la même session
        var groupesExistantsR = await _db.GroupesExamen.ListBySessionAsync(SessionSelectionnee.Session.Id);
        var rolesExistantsR   = await _db.RolesSurveillance.ListBySessionAsync(SessionSelectionnee.Session.Id);

        if (groupesExistantsR.Any(g => g.SurveillantId == SurveillantRoleSelectionne.Id))
        {
            MessageErreur = $"{SurveillantRoleSelectionne.Nom} surveille déjà un groupe dans cette session.";
            return;
        }
        if (rolesExistantsR.Any(r => r.SurveillantId == SurveillantRoleSelectionne.Id))
        {
            MessageErreur = $"{SurveillantRoleSelectionne.Nom} a déjà un rôle de surveillance dans cette session.";
            return;
        }

        var role = new RoleSurveillance
        {
            SessionId     = SessionSelectionnee.Session.Id,
            TypeRole      = TypeRoleInput,
            SurveillantId = SurveillantRoleSelectionne.Id,
            Local         = string.IsNullOrWhiteSpace(LocalRoleInput) ? null : LocalRoleInput.Trim(),
            DureeMinutes  = duree
        };

        await _db.RolesSurveillance.CreateAsync(role);

        SurveillantRoleSelectionne = null;
        LocalRoleInput             = "";
        DureeRoleInput             = "90";

        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    [RelayCommand]
    private async Task SupprimerRoleAsync(RoleSurveillanceDisplay? rd)
    {
        if (rd is null) return;
        await _db.RolesSurveillance.DeleteAsync(rd.Role.Id);
        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Quota minutes : sauvegarder le quota d'un prof
    // ─────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SelectionnerSession(SessionDisplay? sd)
    {
        SessionSelectionnee = sd;
    }

    [RelayCommand]
    private async Task SauvegarderQuotaAsync(ProfMinutesDisplay? pmd)
    {
        if (pmd is null || pmd.MinutesMax <= 0) return;
        await _db.Quotas.UpsertAsync(new QuotaMinutes
        {
            ProfId        = pmd.Prof.Id,
            JourCycle     = 0,
            MinutesMax    = pmd.MinutesMax,
            AnneeScolaire = AppConstants.AnneeScolaire
        });
    }
}
