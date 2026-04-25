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
    GroupeExamen groupe, string epreuve, string enseignant, string surveillant, string salle)
{
    public GroupeExamen Groupe       { get; } = groupe;
    public string       Epreuve      { get; } = epreuve;
    public string       Enseignant   { get; } = enseignant;
    public string       Surveillant  { get; } = surveillant;
    public string       Salle        { get; } = salle;

    public string TypeTag     => Groupe.Type == "Standard" ? "" : $"[{Groupe.Type}]";
    public string TiersTag    => Groupe.TiersTemps ? "+1/3" : "";
    public string ElèvesLabel => $"{Groupe.NbEleves} élèves";
    public string DureeLabel  => $"{Groupe.DureeMinutes} min";
    public string Ligne1      => $"{Epreuve}  {Groupe.CodeGroupe}  {TypeTag}{TiersTag}".Trim();
    public string Ligne2      => $"{Salle}  ·  {Surveillant}  ·  {ElèvesLabel}  ·  {DureeLabel}";
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

public sealed class SessionDisplay(Session session)
{
    public Session                                     Session { get; } = session;
    public ObservableCollection<GroupeExamenDisplay>   Groupes { get; } = new();
    public ObservableCollection<RoleSurveillanceDisplay> Roles { get; } = new();

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
    public string EnTete        => $"{DateLabel}  —  {PeriodeLabel}  ({HeureLabel})";
    public int    TotalGroupes  => Groupes.Count;
    public int    TotalMinutes  => Groupes.Sum(g => g.Groupe.DureeMinutes)
                                 + Roles.Sum(r => r.Role.DureeMinutes);
}

public sealed partial class ProfMinutesDisplay : ObservableObject
{
    public Prof   Prof           { get; init; } = null!;
    [ObservableProperty] private int  _minutesAssignees;
    [ObservableProperty] private int  _minutesMax;

    public int    Pourcentage    => MinutesMax > 0 ? Math.Min(100, MinutesAssignees * 100 / MinutesMax) : 0;
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
//  ViewModel principal
// ─────────────────────────────────────────────────────────────────────────────

public sealed partial class PlanningViewModel : ViewModelBase
{
    private readonly DataContext     _db;
    private readonly PlanningRules   _rules    = new();
    private readonly PlanningSettings _settings = new();

    // Listes principales
    public ObservableCollection<SessionDisplay>      Sessions          { get; } = new();
    public ObservableCollection<ProfMinutesDisplay>  MinutesParProf    { get; } = new();

    // Listes pour les formulaires
    public ObservableCollection<EpreuveDisplay>  EpreuvesDisponibles   { get; } = new();
    public ObservableCollection<Prof>            ProfsDisponibles       { get; } = new();
    public ObservableCollection<Salle>           SallesDisponibles      { get; } = new();

    // ── Formulaire: Nouvelle session ──────────────────────────────────────────
    [ObservableProperty] private DateTime _dateNouvelleSession = DateTime.Today;
    [ObservableProperty] private string   _periodeNouvelleSession = "AM";
    [ObservableProperty] private string   _heureDebutNouvelleSession = "08:30";

    public IReadOnlyList<string> Periodes { get; } = new[] { "AM", "PM" };

    // ── Formulaire: Nouveau groupe dans la session sélectionnée ──────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSessionSelectionnee))]
    private SessionDisplay? _sessionSelectionnee;

    public bool HasSessionSelectionnee => SessionSelectionnee is not null;

    [ObservableProperty] private EpreuveDisplay? _epreuveSelectionnee;
    [ObservableProperty] private Prof?           _enseignantSelectionne;
    [ObservableProperty] private Prof?           _surveillantSelectionne;
    [ObservableProperty] private Salle?          _salleSelectionnee;
    [ObservableProperty] private string          _codeGroupeInput   = "";
    [ObservableProperty] private string          _nbElevesInput     = "25";
    [ObservableProperty] private bool            _tiersTemps;
    [ObservableProperty] private string          _typeGroupe        = "Standard";

    public IReadOnlyList<string> TypesGroupe { get; } = new[] { "Standard", "SAI", "EHDAA" };

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

    // ── Filtre date ───────────────────────────────────────────────────────────
    [ObservableProperty] private DateTime _filtreDate = DateTime.Today;

    partial void OnFiltreDateChanged(DateTime value)
    {
        _ = LoadSessionsAsync();
        _ = LoadMinutesAsync();
    }

    public PlanningViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Chargement
    // ─────────────────────────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        var epreuves = await _db.Epreuves.ListAsync();
        var classes  = await _db.Classes.ListAsync();
        var profs    = await _db.Profs.ListAsync();
        var salles   = await _db.Salles.ListAsync();

        EpreuvesDisponibles.Clear();
        foreach (var e in epreuves)
        {
            var classe = classes.FirstOrDefault(c => c.Id == e.ClasseId);
            EpreuvesDisponibles.Add(new EpreuveDisplay(e, classe?.Nom ?? "—"));
        }

        ProfsDisponibles.Clear();
        foreach (var p in profs) ProfsDisponibles.Add(p);

        SallesDisponibles.Clear();
        foreach (var s in salles) SallesDisponibles.Add(s);

        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    private async Task LoadSessionsAsync()
    {
        var date      = DateOnly.FromDateTime(FiltreDate);
        var sessions  = await _db.Sessions.ListByDateAsync(date);
        var profs     = await _db.Profs.ListAsync();
        var salles    = await _db.Salles.ListAsync();
        var epreuves  = await _db.Epreuves.ListAsync();
        var classes   = await _db.Classes.ListAsync();

        var savedId = SessionSelectionnee?.Session.Id;
        Sessions.Clear();

        foreach (var s in sessions)
        {
            var sd = new SessionDisplay(s);

            var groupes = await _db.GroupesExamen.ListBySessionAsync(s.Id);
            foreach (var g in groupes)
            {
                var ep  = epreuves.FirstOrDefault(x => x.Id == g.EpreuveId);
                var cl  = ep is not null ? classes.FirstOrDefault(c => c.Id == ep.ClasseId) : null;
                var ens = profs.FirstOrDefault(p => p.Id == g.EnseignantId);
                var sur = g.SurveillantId.HasValue ? profs.FirstOrDefault(p => p.Id == g.SurveillantId) : null;
                var sal = g.SalleId.HasValue ? salles.FirstOrDefault(x => x.Id == g.SalleId) : null;

                var epNom = ep is not null
                    ? (cl is not null ? $"{ep.Nom} ({cl.Nom})" : ep.Nom)
                    : "—";

                sd.Groupes.Add(new GroupeExamenDisplay(g, epNom,
                    ens?.Nom ?? "—", sur?.Nom ?? "—", sal?.Nom ?? "—"));
            }

            var roles = await _db.RolesSurveillance.ListBySessionAsync(s.Id);
            foreach (var r in roles)
            {
                var sur = profs.FirstOrDefault(p => p.Id == r.SurveillantId);
                sd.Roles.Add(new RoleSurveillanceDisplay(r, sur?.Nom ?? "—"));
            }

            Sessions.Add(sd);
        }

        // Restaurer la sélection
        if (savedId.HasValue)
            SessionSelectionnee = Sessions.FirstOrDefault(x => x.Session.Id == savedId);
    }

    private async Task LoadMinutesAsync()
    {
        var date   = DateOnly.FromDateTime(FiltreDate);
        var profs  = await _db.Profs.ListAsync();
        var quotas = await _db.Quotas.ListAsync();

        MinutesParProf.Clear();
        foreach (var p in profs)
        {
            var minutes = await _db.GroupesExamen.GetMinutesAssigneesAsync(p.Id, date);
            var quota   = quotas.FirstOrDefault(q => q.ProfId == p.Id);
            MinutesParProf.Add(new ProfMinutesDisplay
            {
                Prof             = p,
                MinutesAssignees = minutes,
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
    private void JourPrecedent() => FiltreDate = FiltreDate.AddDays(-1);

    [RelayCommand]
    private void JourSuivant() => FiltreDate = FiltreDate.AddDays(1);

    [RelayCommand]
    private void AujourdhUi() => FiltreDate = DateTime.Today;

    public string FiltreDateLabel
    {
        get
        {
            var d = DateOnly.FromDateTime(FiltreDate);
            var raw = d.ToString("dddd d MMMM yyyy", new CultureInfo("fr-CA"));
            return char.ToUpper(raw[0]) + raw[1..];
        }
    }

    partial void OnFiltreDateChanging(DateTime value)
    {
        OnPropertyChanged(nameof(FiltreDateLabel));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Créer une session
    // ─────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreerSessionAsync()
    {
        MessageErreur = "";

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
            AnneeScolaire = "2025-2026"
        };

        await _db.Sessions.CreateAsync(session);

        // Synchroniser le filtre sur la date créée
        FiltreDate = DateNouvelleSession;
        await LoadSessionsAsync();
        await LoadMinutesAsync();

        // Sélectionner la nouvelle session
        SessionSelectionnee = Sessions.LastOrDefault();
    }

    [RelayCommand]
    private async Task SupprimerSessionAsync(SessionDisplay? sd)
    {
        if (sd is null) return;
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
    private async Task AjouterGroupeAsync()
    {
        MessageErreur = "";

        if (SessionSelectionnee is null)        { MessageErreur = "Sélectionnez une session."; return; }
        if (EpreuveSelectionnee is null)        { MessageErreur = "Sélectionnez une épreuve.";  return; }
        if (EnseignantSelectionne is null)      { MessageErreur = "Sélectionnez un enseignant."; return; }
        if (!int.TryParse(NbElevesInput, out var nbEleves) || nbEleves < 0)
                                                 { MessageErreur = "Nombre d'élèves invalide."; return; }

        // Vérifier conflit de surveillance dans la même session
        if (SurveillantSelectionne is not null)
        {
            var groupesExistants = await _db.GroupesExamen.ListBySessionAsync(SessionSelectionnee.Session.Id);
            var rolesExistants   = await _db.RolesSurveillance.ListBySessionAsync(SessionSelectionnee.Session.Id);

            if (groupesExistants.Any(g => g.SurveillantId == SurveillantSelectionne.Id))
            {
                MessageErreur = $"{SurveillantSelectionne.Nom} surveille déjà un groupe dans cette session.";
                return;
            }
            if (rolesExistants.Any(r => r.SurveillantId == SurveillantSelectionne.Id))
            {
                MessageErreur = $"{SurveillantSelectionne.Nom} a déjà un rôle de surveillance dans cette session.";
                return;
            }
        }

        var dureeBase = EpreuveSelectionnee.Epreuve.DureeMinutes;
        var dureeEff  = TiersTemps
            ? (int)Math.Ceiling(dureeBase * _settings.TiersTempsMultiplier)
            : dureeBase;

        var groupe = new GroupeExamen
        {
            SessionId     = SessionSelectionnee.Session.Id,
            EpreuveId     = EpreuveSelectionnee.Epreuve.Id,
            CodeGroupe    = CodeGroupeInput.Trim(),
            EnseignantId  = EnseignantSelectionne.Id,
            NbEleves      = nbEleves,
            SurveillantId = SurveillantSelectionne?.Id,
            SalleId       = SalleSelectionnee?.Id,
            TiersTemps    = TiersTemps,
            DureeMinutes  = dureeEff,
            Type          = TypeGroupe
        };

        await _db.GroupesExamen.CreateAsync(groupe);

        // Réinitialiser le formulaire groupe
        EpreuveSelectionnee    = null;
        EnseignantSelectionne  = null;
        SurveillantSelectionne = null;
        SalleSelectionnee      = null;
        CodeGroupeInput        = "";
        NbElevesInput          = "25";
        TiersTemps             = false;
        TypeGroupe             = "Standard";

        await LoadSessionsAsync();
        await LoadMinutesAsync();
    }

    [RelayCommand]
    private async Task SupprimerGroupeAsync(GroupeExamenDisplay? gd)
    {
        if (gd is null) return;
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
            MinutesMax    = pmd.MinutesMax,
            AnneeScolaire = "2025-2026"
        });
    }
}
