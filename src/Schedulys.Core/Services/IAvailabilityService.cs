using Schedulys.Core.Models;

namespace Schedulys.Core.Services;

public interface IAvailabilityService
{
    Task<IReadOnlyList<Prof>> GetAvailableProfsAsync(
        DateOnly date,
        string heureDebut,         // "HH:mm"
        int dureeMinutes,
        bool tiersTemps,
        string? annee = null,      // filtre sur l'année scolaire
        string? role = "Surveillant" // filtre par rôle si tu veux
    );
    Task<IReadOnlyList<Salle>> GetAvailableSallesAsync(
    DateOnly date,
    string heureDebut,
    int dureeMinutes,
    bool tiersTemps,
    int nbEleves,
    string? annee = null
);
}