using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;
public interface ISalleRepository
{
    Task<int> CreateAsync(Salle s);
    Task<Salle?> GetAsync(int id);
    Task<IReadOnlyList<Salle>> ListAsync(string? search = null, string? annee = null);
    Task<bool> UpdateAsync(Salle s);
    Task<bool> DeleteAsync(int id);
}