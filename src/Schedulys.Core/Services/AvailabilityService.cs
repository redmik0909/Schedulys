using System.Globalization;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;

namespace Schedulys.Core.Services;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IProfRepository _profs;
    private readonly ISalleRepository _salles;
    private readonly ICreneauRepository _creneaux;
    private readonly IPlanningRules _rules;
    private readonly PlanningSettings _settings;

    public AvailabilityService(IProfRepository profs,
                               ISalleRepository salles,
                               ICreneauRepository creneaux,
                               IPlanningRules rules,
                               PlanningSettings settings)
    {
        _profs = profs;
        _salles = salles;
        _creneaux = creneaux;
        _rules = rules;
        _settings = settings;
    }

    public async Task<IReadOnlyList<Prof>> GetAvailableProfsAsync(
        DateOnly date, string heureDebut, int dureeMinutes, bool tiersTemps,
        string? annee = null, string? role = "Surveillant")
    {
        // 1) Charger tous les profs (filtrés par rôle/année si besoin)
        var allProfs = await _profs.ListAsync(search: null, annee: annee);
        if (!string.IsNullOrWhiteSpace(role))
            allProfs = allProfs.Where(p => string.Equals(p.Role, role, StringComparison.OrdinalIgnoreCase)).ToList();

        // 2) Charger les créneaux existants pour ce jour
        var existants = await _creneaux.ListByDateAsync(date);

        // 3) Construire le créneau "candidat" (sans salle / sans surveillant pour test de conflit prof)
        var start = DateTime.ParseExact($"{date:yyyy-MM-dd} {heureDebut}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var end = _rules.CalcHeureFin(start, dureeMinutes, tiersTemps, _settings);

        var candidat = new Creneau {
            Date = date.ToString("yyyy-MM-dd"),
            HeureDebut = heureDebut,
            HeureFin = end.ToString("HH:mm"),
            DureeMinutes = tiersTemps ? (int)Math.Ceiling(dureeMinutes * _settings.TiersTempsMultiplier) : dureeMinutes,
            TiersTemps = tiersTemps,
            Statut = "brouillon"
        };

        // 4) Garder seulement les profs qui n'ont pas de conflit
        var libres = new List<Prof>();
        foreach (var prof in allProfs)
        {
            if (!_rules.ProfEnConflit(prof.Id, existants, candidat))
                libres.Add(prof);
        }
        return libres;
    }
    public async Task<IReadOnlyList<Salle>> GetAvailableSallesAsync(
        DateOnly date,
        string heureDebut,
        int dureeMinutes,
        bool tiersTemps,
        int nbEleves,
        string? annee = null)
    {
        // 1) Charger toutes les salles (filtrées par année si besoin)
        var allSalles = await _salles.ListAsync(search: null, annee: annee);

        // 2) Créneaux existants pour cette date
        var existants = await _creneaux.ListByDateAsync(date);

        // 3) Construire le créneau candidat (intervalle horaire)
        var start = DateTime.ParseExact($"{date:yyyy-MM-dd} {heureDebut}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var end = _rules.CalcHeureFin(start, dureeMinutes, tiersTemps, _settings);

        var candidat = new Creneau
        {
            Date = date.ToString("yyyy-MM-dd"),
            HeureDebut = heureDebut,
            HeureFin = end.ToString("HH:mm"),
            DureeMinutes = tiersTemps ? (int)Math.Ceiling(dureeMinutes * _settings.TiersTempsMultiplier) : dureeMinutes,
            TiersTemps = tiersTemps,
            Statut = "brouillon"
        };

        // 4) Garder seulement les salles sans conflit et avec capacité suffisante
        var libres = new List<Salle>();
        foreach (var salle in allSalles)
        {
            if (!_rules.SalleEnConflit(salle.Id, existants, candidat) &&
                _rules.CapaciteValide(salle.Capacite, nbEleves))
            {
                libres.Add(salle);
            }
        }
        return libres;
    }
}