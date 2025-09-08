using Schedulys.Core.Interfaces;
using Schedulys.Data.Db;

namespace Schedulys.Data;

public sealed class DataContext : IDataContext
{
    public IProfRepository Profs { get; }
    public ISalleRepository Salles { get; }
    public IClasseRepository Classes { get; }
    public IEpreuveRepository Epreuves { get; }
    public ICreneauRepository Creneaux { get; }

    public DataContext(string dbPath)
    {
        var factory = new SqliteConnectionFactory(dbPath);
        Profs = new Repositories.ProfRepository(factory);
        Salles = new Repositories.SalleRepository(factory);
        Classes = new Repositories.ClasseRepository(factory);
        Epreuves = new Repositories.EpreuveRepository(factory);
        Creneaux = new Repositories.CreneauRepository(factory);
    }
}