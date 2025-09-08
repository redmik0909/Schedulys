using Schedulys.Core.Models;

namespace Schedulys.Core.Interfaces;

public interface IDataContext
{
    IProfRepository Profs { get; }
    ISalleRepository Salles { get; }
    IClasseRepository Classes { get; }
    IEpreuveRepository Epreuves { get; }
    ICreneauRepository Creneaux { get; }
}