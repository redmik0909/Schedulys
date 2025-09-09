using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IEleveRepository
{
    Task<int> CreateAsync(Eleve e);
    Task<Eleve?> GetAsync(int id);
    Task<IReadOnlyList<Eleve>> ListByClasseAsync(int classeId, bool? tiersTemps = null);
    Task<bool> UpdateAsync(Eleve e);
    Task<bool> DeleteAsync(int id);

    Task<int> CountForClasseAndTTAsync(int classeId, bool tiersTemps);
}