using System.Collections.Generic;
using System.Threading.Tasks;
using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IRoleSurveillanceRepository
{
    Task<int>                              CreateAsync(RoleSurveillance r);
    Task<RoleSurveillance?>               GetAsync(int id);
    Task<IReadOnlyList<RoleSurveillance>> ListBySessionAsync(int sessionId);
    Task<IReadOnlyList<RoleSurveillance>> ListAllAsync();
    Task<bool>                             UpdateAsync(RoleSurveillance r);
    Task<bool>                             DeleteAsync(int id);
}
