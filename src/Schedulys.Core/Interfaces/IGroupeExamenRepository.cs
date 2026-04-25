using System.Collections.Generic;
using System.Threading.Tasks;
using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IGroupeExamenRepository
{
    Task<int>                         CreateAsync(GroupeExamen g);
    Task<GroupeExamen?>               GetAsync(int id);
    Task<IReadOnlyList<GroupeExamen>> ListBySessionAsync(int sessionId);
    Task<IReadOnlyList<GroupeExamen>> ListByPeriodeAsync(DateOnly debut, DateOnly fin);
    Task<int>                         GetMinutesAssigneesAsync(int profId, DateOnly? date = null, string? annee = null);
    Task<bool>                        UpdateAsync(GroupeExamen g);
    Task<bool>                        DeleteAsync(int id);
}
