using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IProfRepository
{
    Task<int> CreateAsync(Prof p);
    Task<Prof?> GetAsync(int id);
    Task<IReadOnlyList<Prof>> ListAsync(string? search = null, string? annee = null);
    Task<bool> UpdateAsync(Prof p);
    Task<bool> DeleteAsync(int id);
}