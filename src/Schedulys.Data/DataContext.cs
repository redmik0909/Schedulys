using Schedulys.Core.Interfaces;
using Schedulys.Data.Db;
using Schedulys.Data.Repositories;

namespace Schedulys.Data;

public sealed class DataContext
{
    private readonly SqliteConnectionFactory _factory;
    internal SqliteConnectionFactory Factory => _factory;

    public DataContext(string dbPath)
    {
        _factory = new SqliteConnectionFactory(dbPath);

        Profs              = new ProfRepository(_factory);
        Salles             = new SalleRepository(_factory);
        Classes            = new ClasseRepository(_factory);
        Epreuves           = new EpreuveRepository(_factory);
        Creneaux           = new CreneauRepository(_factory);
        Eleves             = new EleveRepository(_factory);
        Sessions           = new SessionRepository(_factory);
        GroupesExamen      = new GroupeExamenRepository(_factory);
        RolesSurveillance  = new RoleSurveillanceRepository(_factory);
        Quotas             = new QuotaMinutesRepository(_factory);
    }

    public IProfRepository              Profs             { get; }
    public ISalleRepository             Salles            { get; }
    public IClasseRepository            Classes           { get; }
    public IEpreuveRepository           Epreuves          { get; }
    public ICreneauRepository           Creneaux          { get; }
    public IEleveRepository             Eleves            { get; }
    public ISessionRepository           Sessions          { get; }
    public IGroupeExamenRepository      GroupesExamen     { get; }
    public IRoleSurveillanceRepository  RolesSurveillance { get; }
    public IQuotaMinutesRepository      Quotas            { get; }
}
