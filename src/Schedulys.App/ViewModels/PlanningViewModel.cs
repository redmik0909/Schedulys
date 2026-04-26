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
//  ViewModel principal
// ─────────────────────────────────────────────────────────────────────────────

public sealed partial class PlanningViewModel : ViewModelBase
{
    private readonly DataContext     _db;
    private readonly PlanningSettings _settings = new();

    // Listes principales
    public ObservableCollection<SessionDisplay>      Sessions          { get; } = new();
    public ObservableCollection<ProfMinutesDisplay>  MinutesParProf    { get; } = new();

    // Listes pour les formulaires
    public ObservableCollection<Classe>          ClassesDisponibles     { get; } = new();
    public ObservableCollection<EpreuveDisplay>  EpreuvesDisponibles    { get; } = new();
    public ObservableCollection<Prof>            ProfsDisponibles        { get; } = new();
    public ObservableCollection<Salle>           SallesDisponibles       { get; } = new();

    // ── Formulaire: Nouvelle session ──────────────────────────────────────────
    [ObservableProperty] private DateTime _dateNouvelleSession = DateTime.Today;
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

    [ObservableProperty] private Classe?          _classeSelectionnee;
    [ObservableProperty] private EpreuveDisplay?  _epreuveSelectionnee;
    [ObservableProperty] private Prof?            _enseignantSelectionne;
    [ObservableProperty] private Prof?            _surveillantSelectionne;
    [ObservableProperty] private Salle?           _salleSelectionnee;
    [ObservableProperty] private string           _codeGroupeInput   = "";
    [ObservableProperty] private string           _nbElevesInput     = "25";
    [ObservableProperty] private string           _heureFinInput     = "11:30";
    [ObservableProperty] private string           _premierDepartInput = "10:30";
    [ObservableProperty] private bool             _tiersTemps;
    [ObservableProperty] private string           _typeGroupe        = "Standard";

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

    public PlanningViewModel(DataContext db)
    {
        _db = db;
        _jourCycleSession = JoursDuCycle[0];
        _ = LoadAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Chargement
    // ─────────────────────────────────────────────────────────────────────────

    partial void OnClasseSelectionneeChanged(Classe? value)
    {
        if (value is null) return;
        CodeGroupeInput        = value.Code;
        NbElevesInput          = value.Effectif > 0 ? value.Effectif.ToString() : NbElevesInput;
        EnseignantSelectionne  = ProfsDisponibles.FirstOrDefault(p => p.Id == value.ProfId);
    }

    private async Task LoadAsync()
    {
        var classes = await _db.Classes.ListAsync();
        var profs   = await _db.Profs.ListAsync();
        var salles  = await _db.Salles.ListAsync();

        ClassesDisponibles.Clear();
        foreach (var c in classes.OrderBy(c => c.Code)) ClassesDisponibles.Add(c);

        var epreuves = await _db.Epreuves.ListAsync();
        EpreuvesDisponibles.Clear();
        foreach (var e in epreuves)
        {
            var cl = classes.FirstOrDefault(c => c.Id == e.ClasseId);
            EpreuvesDisponibles.Add(new EpreuveDisplay(e, cl?.Nom ?? "—"));
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
                    var cl = ep is not null ? classes.FirstOrDefault(c => c.Id == ep.ClasseId) : null;
                    description = ep is not null
                        ? (cl is not null ? $"{ep.Nom} ({cl.Nom})" : ep.Nom)
                        : g.CodeGroupe;
                }

                var ens = profs.FirstOrDefault(p => p.Id == g.EnseignantId);
                var sur = g.SurveillantId.HasValue ? profs.FirstOrDefault(p => p.Id == g.SurveillantId) : null;
                var sal = g.SalleId.HasValue ? salles.FirstOrDefault(x => x.Id == g.SalleId) : null;

                sd.Groupes.Add(new GroupeExamenDisplay(g, description,
                    ens?.Nom ?? "—", sur?.Nom ?? "—", sal?.Nom ?? "—", s.HeureDebut));
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
        var date     = DateOnly.FromDateTime(FiltreDate);
        var profs    = await _db.Profs.ListAsync();
        var quotas   = await _db.Quotas.ListAsync();
        var sessions = await _db.Sessions.ListByDateAsync(date);

        // Déterminer le jour de cycle de la journée (premier trouvé non nul)
        var jourCycle = sessions.FirstOrDefault(s => s.JourCycle > 0)?.JourCycle ?? 0;

        MinutesParProf.Clear();
        foreach (var p in profs)
        {
            var minutes = await _db.GroupesExamen.GetMinutesAssigneesAsync(p.Id, date);
            // Quota spécifique au jour de cycle, sinon repli sur la moyenne (JourCycle=0)
            var quota = (jourCycle > 0
                ? quotas.FirstOrDefault(q => q.ProfId == p.Id && q.JourCycle == jourCycle)
                : null)
                ?? quotas.FirstOrDefault(q => q.ProfId == p.Id && q.JourCycle == 0);
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
    private void Aujourdhui() => FiltreDate = DateTime.Today;

    public string FiltreDateLabel
    {
        get
        {
            var d = DateOnly.FromDateTime(FiltreDate);
            var raw = d.ToString("dddd d MMMM yyyy", new CultureInfo("fr-CA"));
            return char.ToUpper(raw[0]) + raw[1..];
        }
    }

    partial void OnFiltreDateChanged(DateTime value)
    {
        OnPropertyChanged(nameof(FiltreDateLabel));
        _ = LoadSessionsAsync();
        _ = LoadMinutesAsync();
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
            JourCycle     = JourCycleSession?.Valeur ?? 0,
            AnneeScolaire = AppConstants.AnneeScolaire
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

        if (SessionSelectionnee is null)   { MessageErreur = "Sélectionnez une session."; return; }
        if (ClasseSelectionnee is null)    { MessageErreur = "Sélectionnez un groupe.";  return; }
        if (!int.TryParse(NbElevesInput, out var nbEleves) || nbEleves < 0)
                                            { MessageErreur = "Nombre d'élèves invalide."; return; }
        if (!TimeSpan.TryParse(HeureFinInput, out var heureFinTs))
                                            { MessageErreur = "Heure de fin invalide (HH:mm)."; return; }
        if (!TimeSpan.TryParse(SessionSelectionnee.Session.HeureDebut, out var heureDebutTs))
                                            { MessageErreur = "Heure de début de la session invalide."; return; }
        var dureeMinutes = (int)(heureFinTs - heureDebutTs).TotalMinutes;
        if (dureeMinutes <= 0)              { MessageErreur = "L'heure de fin doit être après l'heure de début."; return; }

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

        var groupe = new GroupeExamen
        {
            SessionId      = SessionSelectionnee.Session.Id,
            EpreuveId      = 0,
            ClasseId       = ClasseSelectionnee.Id,
            CodeGroupe     = ClasseSelectionnee.Code,
            EnseignantId   = ClasseSelectionnee.ProfId,
            NbEleves       = nbEleves,
            SurveillantId  = SurveillantSelectionne?.Id,
            SalleId        = SalleSelectionnee?.Id,
            TiersTemps     = false,
            DureeMinutes   = dureeMinutes,
            Type           = "Standard",
            HeureFin       = HeureFinInput.Trim(),
            PremierDepart  = PremierDepartInput.Trim()
        };

        await _db.GroupesExamen.CreateAsync(groupe);

        // Réinitialiser le formulaire groupe
        ClasseSelectionnee     = null;
        EnseignantSelectionne  = null;
        SurveillantSelectionne = null;
        SalleSelectionnee      = null;
        CodeGroupeInput        = "";
        NbElevesInput          = "25";
        HeureFinInput          = "11:30";
        PremierDepartInput     = "10:30";

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
            JourCycle     = 0,
            MinutesMax    = pmd.MinutesMax,
            AnneeScolaire = AppConstants.AnneeScolaire
        });
    }
}
