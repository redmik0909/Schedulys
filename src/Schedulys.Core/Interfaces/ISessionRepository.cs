using System.Collections.Generic;
using System.Threading.Tasks;
using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface ISessionRepository
{
    Task<int>                    CreateAsync(Session s);
    Task<Session?>               GetAsync(int id);
    Task<IReadOnlyList<Session>> ListAsync(string? annee = null);
    Task<IReadOnlyList<Session>> ListByDateAsync(DateOnly date);
    Task<IReadOnlyList<Session>> ListByPeriodeAsync(DateOnly debut, DateOnly fin);
    Task<bool>                   UpdateAsync(Session s);
    Task<bool>                   DeleteAsync(int id);
}
