using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;
public interface IClasseRepository
{
    Task<int> CreateAsync(Classe c);
    Task<Classe?> GetAsync(int id);
    Task<IReadOnlyList<Classe>> ListAsync(string? search = null, string? annee = null);
    Task<bool> UpdateAsync(Classe c);
    Task<bool> DeleteAsync(int id);
}