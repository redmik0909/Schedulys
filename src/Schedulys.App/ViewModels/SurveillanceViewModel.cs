using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed class SessionSurveillanceItem
{
    public Session Session { get; }
    public string  Label
    {
        get
        {
            if (!DateOnly.TryParse(Session.Date, out var d)) return Session.Date;
            var raw     = d.ToString("ddd d MMM yyyy", new CultureInfo("fr-CA"));
            var dateStr = char.ToUpper(raw[0]) + raw[1..];
            var periode = Session.Periode == "AM" ? "Matin" : "Après-midi";
            return Session.JourCycle > 0
                ? $"{dateStr}  —  {periode}  ({Session.HeureDebut})  —  Jour {Session.JourCycle}"
                : $"{dateStr}  —  {periode}  ({Session.HeureDebut})";
        }
    }

    public SessionSurveillanceItem(Session s) => Session = s;
}

public sealed class RoleSurveillanceRow
{
    public RoleSurveillance Role        { get; }
    public string           Surveillant { get; }
    public string           Ligne1      => Role.TypeRole;
    public string           Ligne2
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Role.Local)) parts.Add($"Local {Role.Local}");
            parts.Add($"{Role.DureeMinutes} min");
            return $"{Surveillant}  ·  {string.Join("  ·  ", parts)}";
        }
    }

    public RoleSurveillanceRow(RoleSurveillance role, string surveillant)
    {
        Role        = role;
        Surveillant = surveillant;
    }
}

public sealed class SessionSurveillanceGroup
{
    public string                                    EnTete { get; }
    public ObservableCollection<RoleSurveillanceRow> Roles  { get; } = new();

    public SessionSurveillanceGroup(Session session)
    {
        if (!DateOnly.TryParse(session.Date, out var d))
        {
            EnTete = session.Date;
            return;
        }
        var raw     = d.ToString("dddd d MMMM yyyy", new CultureInfo("fr-CA"));
        var dateStr = char.ToUpper(raw[0]) + raw[1..];
        var periode = session.Periode == "AM" ? "Matin" : "Après-midi";
        EnTete = session.JourCycle > 0
            ? $"{dateStr}  —  {periode}  ({session.HeureDebut})  —  Jour {session.JourCycle}"
            : $"{dateStr}  —  {periode}  ({session.HeureDebut})";
    }
}

public sealed partial class SurveillanceViewModel : ViewModelBase
{
    private readonly DataContext _db;

    // Listes formulaire
    public ObservableCollection<SessionSurveillanceItem> SessionsDisponibles { get; } = new();
    public ObservableCollection<Prof>                    ProfsDisponibles    { get; } = new();

    public static IReadOnlyList<string> TypesRole { get; } = new[]
    {
        "Surveillance 1er étage",
        "Surveillance 3e étage",
        "Surveillance bibliothèque SAI",
        "Disponibilités et pauses"
    };

    // Champs formulaire
    [ObservableProperty] private SessionSurveillanceItem? _sessionSelectionnee;
    [ObservableProperty] private string  _typeRoleInput         = "Surveillance 1er étage";
    [ObservableProperty] private Prof?   _surveillantSelectionne;
    [ObservableProperty] private string  _localInput            = "";
    [ObservableProperty] private string  _dureeInput            = "90";

    // Liste affichage
    public ObservableCollection<SessionSurveillanceGroup> GroupesParSession { get; } = new();

    [ObservableProperty] private string _erreur  = "";
    [ObservableProperty] private string _message = "";

    public SurveillanceViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var sessions = await _db.Sessions.ListAsync();
        var roles    = await _db.RolesSurveillance.ListAllAsync();
        var profs    = await _db.Profs.ListAsync();

        var profsMap = profs.ToDictionary(p => p.Id);

        // Formulaire
        var savedSession = SessionSelectionnee?.Session.Id;
        SessionsDisponibles.Clear();
        foreach (var s in sessions.OrderBy(s => s.Date).ThenBy(s => s.Periode))
            SessionsDisponibles.Add(new SessionSurveillanceItem(s));
        if (savedSession.HasValue)
            SessionSelectionnee = SessionsDisponibles.FirstOrDefault(i => i.Session.Id == savedSession);

        ProfsDisponibles.Clear();
        foreach (var p in profs.Where(p => p.Role == "Surveillant").OrderBy(p => p.Nom))
            ProfsDisponibles.Add(p);

        // Liste groupée
        GroupesParSession.Clear();
        foreach (var session in sessions.OrderBy(s => s.Date).ThenBy(s => s.HeureDebut))
        {
            var sessionRoles = roles.Where(r => r.SessionId == session.Id).ToList();
            if (sessionRoles.Count == 0) continue;

            var group = new SessionSurveillanceGroup(session);
            foreach (var r in sessionRoles)
            {
                var nomSurv = profsMap.TryGetValue(r.SurveillantId, out var p) ? p.Nom : "—";
                group.Roles.Add(new RoleSurveillanceRow(r, nomSurv));
            }
            GroupesParSession.Add(group);
        }
    }

    [RelayCommand]
    private async Task AjouterRoleAsync()
    {
        Erreur  = "";
        Message = "";

        if (SessionSelectionnee is null)     { Erreur = "Sélectionnez une session.";    return; }
        if (SurveillantSelectionne is null)  { Erreur = "Sélectionnez un surveillant."; return; }
        if (!int.TryParse(DureeInput, out var duree) || duree <= 0)
                                              { Erreur = "Durée invalide (minutes).";    return; }

        // Vérifier qu'il n'est pas déjà assigné dans cette session
        var groupesExistants = await _db.GroupesExamen.ListBySessionAsync(SessionSelectionnee.Session.Id);
        var rolesExistants   = await _db.RolesSurveillance.ListBySessionAsync(SessionSelectionnee.Session.Id);

        if (groupesExistants.Any(g => g.SurveillantId == SurveillantSelectionne.Id))
        {
            Erreur = $"{SurveillantSelectionne.Nom} surveille déjà un groupe dans cette session.";
            return;
        }
        if (rolesExistants.Any(r => r.SurveillantId == SurveillantSelectionne.Id))
        {
            Erreur = $"{SurveillantSelectionne.Nom} a déjà un rôle dans cette session.";
            return;
        }

        await _db.RolesSurveillance.CreateAsync(new RoleSurveillance
        {
            SessionId     = SessionSelectionnee.Session.Id,
            TypeRole      = TypeRoleInput,
            SurveillantId = SurveillantSelectionne.Id,
            Local         = string.IsNullOrWhiteSpace(LocalInput) ? null : LocalInput.Trim(),
            DureeMinutes  = duree
        });

        Message                = "✓ Rôle ajouté.";
        SurveillantSelectionne = null;
        LocalInput             = "";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SupprimerRoleAsync(RoleSurveillanceRow? row)
    {
        if (row is null) return;
        Erreur  = "";
        Message = "";
        try
        {
            await _db.RolesSurveillance.DeleteAsync(row.Role.Id);
            Message = "✓ Rôle supprimé.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Erreur = $"Erreur : {ex.Message}";
        }
    }
}
