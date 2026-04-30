using System.Collections.Generic;
using System.Threading.Tasks;
using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IZoneSurveillanceRepository
{
    Task<int>                              CreateAsync(ZoneSurveillance z);
    Task<IReadOnlyList<ZoneSurveillance>> ListAsync();
    Task<bool>                             UpdateAsync(ZoneSurveillance z);
    Task<bool>                             DeleteAsync(int id);
}
