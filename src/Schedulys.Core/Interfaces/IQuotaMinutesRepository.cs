using System.Collections.Generic;
using System.Threading.Tasks;
using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IQuotaMinutesRepository
{
    Task<int>                           CreateAsync(QuotaMinutes q);
    Task<QuotaMinutes?>                 GetByProfAsync(int profId, string? annee = null);
    Task<IReadOnlyList<QuotaMinutes>>   ListAsync(string? annee = null);
    Task<bool>                          UpsertAsync(QuotaMinutes q);
    Task<bool>                          DeleteAsync(int id);
}
