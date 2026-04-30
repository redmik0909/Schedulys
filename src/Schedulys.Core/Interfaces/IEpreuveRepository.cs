using System.Collections.Generic;
using System.Threading.Tasks;
using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;
public interface IEpreuveRepository
{
    Task<int> CreateAsync(Epreuve e);
    Task<Epreuve?> GetAsync(int id);
    Task<IReadOnlyList<Epreuve>> ListAsync(string? search = null, string? annee = null);
    Task<bool> UpdateAsync(Epreuve e);
    Task<bool> DeleteAsync(int id);

    // Relations N-N épreuves ↔ groupes
    Task<IReadOnlyList<int>> GetGroupeIdsAsync(int epreuveId);
    Task SetGroupesAsync(int epreuveId, IEnumerable<int> classeIds);
    Task<IReadOnlyList<Epreuve>> GetByClasseAsync(int classeId, string? annee = null);
}