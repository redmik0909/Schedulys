using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;
public interface IEpreuveRepository
{
    Task<int> CreateAsync(Epreuve e);
    Task<Epreuve?> GetAsync(int id);
    Task<IReadOnlyList<Epreuve>> ListAsync(
        int? classeId = null, string? search = null, string? annee = null);
    Task<bool> UpdateAsync(Epreuve e);
    Task<bool> DeleteAsync(int id);
}