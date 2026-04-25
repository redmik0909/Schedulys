using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;
public interface ICreneauRepository
{
    Task<int> CreateAsync(Creneau c);
    Task<Creneau?> GetAsync(int id);
    Task<IReadOnlyList<Creneau>> ListByDateAsync(DateOnly date, string? annee = null);
    Task<IReadOnlyList<Creneau>> ListBySemaineAsync(DateOnly startOfWeek, string? annee = null);
    Task<IReadOnlyList<Creneau>> ListByPeriodeAsync(DateOnly debut, DateOnly fin);
    Task<bool> UpdateAsync(Creneau c);
    Task<bool> DeleteAsync(int id);
}