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

    // Colonnes TiersTemps + DureeMinutes sur Creneaux (si absentes)
    await AddColumnIfMissing(cn, "Creneaux", "TiersTemps", "INTEGER NOT NULL DEFAULT 0");
    await AddColumnIfMissing(cn, "Creneaux", "DureeMinutes", "INTEGER NOT NULL DEFAULT 0");
      const string createEleves = @"
  CREATE TABLE IF NOT EXISTS Eleves (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Nom TEXT NOT NULL,
    ClasseId INTEGER NOT NULL,
    TiersTemps INTEGER NOT NULL DEFAULT 0,
    Annee TEXT NOT NULL,
    FOREIGN KEY(ClasseId) REFERENCES Classes(Id)
  );";
  await cn.ExecuteAsync(createEleves);    
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