using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Schedulys.Data.Db;

public static class SchemaInitializer
{
  public static async Task InitAsync(SqliteConnectionFactory factory)
  {
    using var cn = factory.Create();
    await cn.OpenAsync();

    var ddl = @"
CREATE TABLE IF NOT EXISTS Profs(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom TEXT NOT NULL,
  Role TEXT NOT NULL,
  Annee TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Salles(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom TEXT NOT NULL,
  Capacite INTEGER NOT NULL,
  Type TEXT,
  Annee TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Classes(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom TEXT NOT NULL,
  Effectif INTEGER NOT NULL,
  Annee TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Epreuves(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom TEXT NOT NULL,
  ClasseId INTEGER NOT NULL,
  DureeMinutes INTEGER NOT NULL,
  TiersTemps INTEGER NOT NULL DEFAULT 0,
  Ministerielle INTEGER NOT NULL DEFAULT 0,
  Annee TEXT NOT NULL,
  FOREIGN KEY (ClasseId) REFERENCES Classes(Id)
);
CREATE TABLE IF NOT EXISTS Creneaux(
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  EpreuveId INTEGER NOT NULL,
  SalleId INTEGER NOT NULL,
  SurveillantId INTEGER NOT NULL,
  Date TEXT NOT NULL,
  HeureDebut TEXT NOT NULL,
  HeureFin TEXT NOT NULL,
  Statut TEXT NOT NULL DEFAULT 'brouillon',
  FOREIGN KEY (EpreuveId) REFERENCES Epreuves(Id),
  FOREIGN KEY (SalleId) REFERENCES Salles(Id),
  FOREIGN KEY (SurveillantId) REFERENCES Profs(Id)
);
CREATE INDEX IF NOT EXISTS IDX_Creneaux_SalleDate ON Creneaux(SalleId, Date, HeureDebut, HeureFin);
CREATE INDEX IF NOT EXISTS IDX_Creneaux_ProfDate  ON Creneaux(SurveillantId, Date, HeureDebut, HeureFin);";

    await cn.ExecuteAsync(ddl);

    // Colonnes TiersTemps + DureeMinutes sur Creneaux (si absentes — rétrocompat)
    await AddColumnIfMissing(cn, "Creneaux", "TiersTemps",   "INTEGER NOT NULL DEFAULT 0");
    await AddColumnIfMissing(cn, "Creneaux", "DureeMinutes", "INTEGER NOT NULL DEFAULT 0");

    const string newTables = @"
CREATE TABLE IF NOT EXISTS Eleves (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom TEXT NOT NULL,
  ClasseId INTEGER NOT NULL,
  TiersTemps INTEGER NOT NULL DEFAULT 0,
  Annee TEXT NOT NULL,
  FOREIGN KEY(ClasseId) REFERENCES Classes(Id)
);

CREATE TABLE IF NOT EXISTS Sessions (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  Date          TEXT    NOT NULL,
  Periode       TEXT    NOT NULL DEFAULT 'AM',
  HeureDebut    TEXT    NOT NULL,
  AnneeScolaire TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS GroupesExamen (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  SessionId     INTEGER NOT NULL,
  EpreuveId     INTEGER NOT NULL,
  CodeGroupe    TEXT    NOT NULL DEFAULT '',
  EnseignantId  INTEGER NOT NULL DEFAULT 0,
  NbEleves      INTEGER NOT NULL DEFAULT 0,
  SurveillantId INTEGER,
  SalleId       INTEGER,
  TiersTemps    INTEGER NOT NULL DEFAULT 0,
  DureeMinutes  INTEGER NOT NULL DEFAULT 0,
  Type          TEXT    NOT NULL DEFAULT 'Standard',
  FOREIGN KEY (SessionId)    REFERENCES Sessions(Id),
  FOREIGN KEY (EpreuveId)    REFERENCES Epreuves(Id)
);

CREATE TABLE IF NOT EXISTS RolesSurveillance (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  SessionId     INTEGER NOT NULL,
  TypeRole      TEXT    NOT NULL,
  SurveillantId INTEGER NOT NULL,
  Local         TEXT,
  DureeMinutes  INTEGER NOT NULL DEFAULT 0,
  FOREIGN KEY (SessionId) REFERENCES Sessions(Id)
);

CREATE TABLE IF NOT EXISTS QuotasMinutes (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  ProfId        INTEGER NOT NULL,
  MinutesMax    INTEGER NOT NULL,
  AnneeScolaire TEXT    NOT NULL,
  UNIQUE(ProfId, AnneeScolaire)
);

CREATE INDEX IF NOT EXISTS IDX_Sessions_Date          ON Sessions(Date);
CREATE INDEX IF NOT EXISTS IDX_GroupesExamen_Session  ON GroupesExamen(SessionId);
CREATE INDEX IF NOT EXISTS IDX_GroupesExamen_Surv     ON GroupesExamen(SurveillantId);
CREATE INDEX IF NOT EXISTS IDX_GroupesExamen_Ens      ON GroupesExamen(EnseignantId);
CREATE INDEX IF NOT EXISTS IDX_RolesSurv_Session      ON RolesSurveillance(SessionId);
CREATE INDEX IF NOT EXISTS IDX_RolesSurv_Surv         ON RolesSurveillance(SurveillantId);
CREATE INDEX IF NOT EXISTS IDX_Quotas_Prof            ON QuotasMinutes(ProfId);
";
    await cn.ExecuteAsync(newTables);

    // Migration Niveau sur Classes (legacy)
    await AddColumnIfMissing(cn, "Classes", "Niveau", "INTEGER NOT NULL DEFAULT 0");

    // Migration catalogue groupes
    await AddColumnIfMissing(cn, "Classes", "Code",        "TEXT NOT NULL DEFAULT ''");
    await AddColumnIfMissing(cn, "Classes", "Description", "TEXT NOT NULL DEFAULT ''");
    await AddColumnIfMissing(cn, "Classes", "ProfId",      "INTEGER NOT NULL DEFAULT 0");

    // Migration GroupesExamen
    await AddColumnIfMissing(cn, "GroupesExamen", "ClasseId",      "INTEGER");
    await AddColumnIfMissing(cn, "GroupesExamen", "HeureFin",      "TEXT NOT NULL DEFAULT ''");
    await AddColumnIfMissing(cn, "GroupesExamen", "PremierDepart", "TEXT NOT NULL DEFAULT ''");

    // Migration JourCycle : Sessions
    await AddColumnIfMissing(cn, "Sessions", "JourCycle", "INTEGER NOT NULL DEFAULT 0");

    // Migration JourCycle : QuotasMinutes — recrée la table avec le nouveau UNIQUE(ProfId, JourCycle, AnneeScolaire)
    var hasJourCycleQuotas = await cn.ExecuteScalarAsync<long>(
        "SELECT COUNT(*) FROM pragma_table_info('QuotasMinutes') WHERE name='JourCycle';");
    if (hasJourCycleQuotas == 0)
    {
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("ALTER TABLE QuotasMinutes RENAME TO QuotasMinutes_old", transaction: tx);
        await cn.ExecuteAsync(@"
CREATE TABLE QuotasMinutes (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  ProfId        INTEGER NOT NULL,
  JourCycle     INTEGER NOT NULL DEFAULT 0,
  MinutesMax    INTEGER NOT NULL,
  AnneeScolaire TEXT    NOT NULL,
  UNIQUE(ProfId, JourCycle, AnneeScolaire)
);", transaction: tx);
        await cn.ExecuteAsync(@"
INSERT INTO QuotasMinutes(Id, ProfId, JourCycle, MinutesMax, AnneeScolaire)
SELECT Id, ProfId, 0, MinutesMax, AnneeScolaire FROM QuotasMinutes_old;", transaction: tx);
        await cn.ExecuteAsync("DROP TABLE QuotasMinutes_old", transaction: tx);
        await cn.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IDX_Quotas_Prof ON QuotasMinutes(ProfId)", transaction: tx);
        tx.Commit();
    }
  }

    private static async Task AddColumnIfMissing(SqliteConnection cn, string table, string column, string sqlType)
    {
        var exists = await cn.ExecuteScalarAsync<long>(
            @"SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name=@column;",
            new { table, column });
        if (exists == 0)
        {
            var alter = $"ALTER TABLE {table} ADD COLUMN {column} {sqlType};";
            await cn.ExecuteAsync(alter);
        }
    }
}