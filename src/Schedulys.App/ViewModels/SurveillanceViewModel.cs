using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed class RoleSurveillanceRow
{
    public RoleSurveillance Role        { get; }
    public string           Surveillant { get; }
    public string           Ligne1      => Role.TypeRole;
    public string           Ligne2
    {
        get
        {
            var plage = (!string.IsNullOrWhiteSpace(Role.HeureDebut) && !string.IsNullOrWhiteSpace(Role.HeureFin))
                ? $"{Role.HeureDebut}–{Role.HeureFin}  ({Role.DureeMinutes} min)"
                : $"{Role.DureeMinutes} min";
            return $"{Surveillant}  ·  {plage}";
        }
    }

    public RoleSurveillanceRow(RoleSurveillance role, string surveillant)
    {
        Role        = role;
        Surveillant = surveillant;
    }
}

public sealed class JourSurveillanceGroup
{
    public string                                    EnTete { get; }
    public ObservableCollection<RoleSurveillanceRow> Roles  { get; } = new();

    public JourSurveillanceGroup(string date)
    {
        if (DateOnly.TryParse(date, out var d))
        {
            var raw = d.ToString("dddd d MMMM yyyy", new CultureInfo("fr-CA"));
            EnTete = char.ToUpper(raw[0]) + raw[1..];
        }
        else
        {
            EnTete = string.IsNullOrWhiteSpace(date) ? "Date inconnue" : date;
        }
    }
}

public sealed partial class SurveillanceViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<Prof>   ProfsDisponibles { get; } = new();
    public ObservableCollection<string> ZonesDisponibles { get; } = new();

    [ObservableProperty] private DateTime _dateRole = DateTime.Today;
    [ObservableProperty] private string   _typeRoleInput          = "";
    [ObservableProperty] private Prof?    _surveillantSelectionne;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlageCalculee))]
    private string _heureDebutInput = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlageCalculee))]
    private string _heureFinInput = "";

    public string PlageCalculee
    {
        get
        {
            if (!TimeSpan.TryParse(HeureDebutInput, out var debut) ||
                !TimeSpan.TryParse(HeureFinInput,   out var fin)   || fin <= debut)
                return "";
            var duree = (int)(fin - debut).TotalMinutes;
            return $"{duree} min";
        }
    }

    public ObservableCollection<JourSurveillanceGroup> GroupesParJour { get; } = new();

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
        var roles = await _db.RolesSurveillance.ListAllAsync();
        var profs = await _db.Profs.ListAsync();
        var zones = await _db.Zones.ListAsync();

        ZonesDisponibles.Clear();
        foreach (var z in zones) ZonesDisponibles.Add(z.Nom);
        if (string.IsNullOrEmpty(TypeRoleInput) && ZonesDisponibles.Count > 0)
            TypeRoleInput = ZonesDisponibles[0];

        ProfsDisponibles.Clear();
        foreach (var p in profs.Where(p => p.Role == "Surveillant").OrderBy(p => p.Nom))
            ProfsDisponibles.Add(p);

        var profsMap = profs.ToDictionary(p => p.Id);

        GroupesParJour.Clear();
        foreach (var grp in roles.GroupBy(r => r.Date).OrderBy(g => g.Key))
        {
            var group = new JourSurveillanceGroup(grp.Key);
            foreach (var r in grp.OrderBy(r => r.HeureDebut))
            {
                var nomSurv = profsMap.TryGetValue(r.SurveillantId, out var p) ? p.Nom : "—";
                group.Roles.Add(new RoleSurveillanceRow(r, nomSurv));
            }
            GroupesParJour.Add(group);
        }
    }

    [RelayCommand]
    private async Task AjouterRoleAsync()
    {
        Erreur  = "";
        Message = "";

        if (SurveillantSelectionne is null) { Erreur = "Sélectionnez un surveillant.";           return; }
        if (!TimeSpan.TryParse(HeureDebutInput, out var tsDebut))
                                            { Erreur = "Heure de début invalide (HH:mm).";       return; }
        if (!TimeSpan.TryParse(HeureFinInput, out var tsFin) || tsFin <= tsDebut)
                                            { Erreur = "Heure de fin invalide ou antérieure au début."; return; }

        var duree   = (int)(tsFin - tsDebut).TotalMinutes;
        var dateStr = DateOnly.FromDateTime(DateRole).ToString("yyyy-MM-dd");

        // Vérifier qu'il n'est pas déjà assigné à ce jour
        var tousLesRoles = await _db.RolesSurveillance.ListAllAsync();
        if (tousLesRoles.Any(r => r.Date == dateStr && r.SurveillantId == SurveillantSelectionne.Id))
        {
            Erreur = $"{SurveillantSelectionne.Nom} a déjà un rôle de surveillance ce jour.";
            return;
        }

        await _db.RolesSurveillance.CreateAsync(new RoleSurveillance
        {
            SessionId     = 0,
            Date          = dateStr,
            TypeRole      = TypeRoleInput,
            SurveillantId = SurveillantSelectionne.Id,
            HeureDebut    = HeureDebutInput.Trim(),
            HeureFin      = HeureFinInput.Trim(),
            DureeMinutes  = duree
        });

        Message                = "✓ Rôle ajouté.";
        SurveillantSelectionne = null;
        HeureDebutInput        = "";
        HeureFinInput          = "";
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
