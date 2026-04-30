using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Schedulys.Data.Db;

public static class SchemaInitializer
{
  public static async Task InitAsync(SqliteConnectionFactory factory, Action<string>? log = null)
  {
    using var cn = factory.Create();
    await cn.OpenAsync();

    log?.Invoke("Connexion ouverte.");
    // WAL : meilleure résistance aux crashs, lectures concurrentes sans blocage
    await cn.ExecuteAsync("PRAGMA journal_mode=WAL;");
    // FK désactivées pendant les migrations (Microsoft.Data.Sqlite 8+ les active par défaut)
    await cn.ExecuteAsync("PRAGMA foreign_keys = OFF;");

    // Récupération des tables orphelines issues d'une migration interrompue par un crash
    foreach (var baseName in new[] { "GroupesExamen", "Epreuves", "QuotasMinutes" })
    {
        var oldName  = $"{baseName}_old";
        var oldExist = await cn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name", new { name = oldName });
        if (oldExist == 0) continue;

        var newExist = await cn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name", new { name = baseName });

        if (newExist > 0)
        {
            // Migration terminée avant le crash — on nettoie juste la table temporaire
            log?.Invoke($"Nettoyage table orpheline : DROP {oldName}");
            await cn.ExecuteAsync($"DROP TABLE {oldName}");
        }
        else
        {
            // Migration interrompue avant la création de la nouvelle table — on restaure
            log?.Invoke($"Récupération migration interrompue : RENAME {oldName} → {baseName}");
            await cn.ExecuteAsync($"ALTER TABLE {oldName} RENAME TO {baseName}");
        }
    }

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
  EpreuveId     INTEGER NOT NULL DEFAULT 0,
  CodeGroupe    TEXT    NOT NULL DEFAULT '',
  EnseignantId  INTEGER NOT NULL DEFAULT 0,
  NbEleves      INTEGER NOT NULL DEFAULT 0,
  SurveillantId INTEGER,
  SalleId       INTEGER,
  TiersTemps    INTEGER NOT NULL DEFAULT 0,
  DureeMinutes  INTEGER NOT NULL DEFAULT 0,
  Type          TEXT    NOT NULL DEFAULT 'Standard',
  FOREIGN KEY (SessionId) REFERENCES Sessions(Id)
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
  JourCycle     INTEGER NOT NULL DEFAULT 0,
  MinutesMax    INTEGER NOT NULL,
  AnneeScolaire TEXT    NOT NULL,
  UNIQUE(ProfId, JourCycle, AnneeScolaire)
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

    // Migration : suppression FK EpreuveId→Epreuves dans GroupesExamen
    var geCreateSql = await cn.ExecuteScalarAsync<string>(
        "SELECT sql FROM sqlite_master WHERE type='table' AND name='GroupesExamen'");
    if (geCreateSql?.Contains("Epreuves") == true)
    {
        log?.Invoke("Migration GroupesExamen : suppression FK EpreuveId → démarrage.");
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("ALTER TABLE GroupesExamen RENAME TO GroupesExamen_old", transaction: tx);
        await cn.ExecuteAsync(@"
CREATE TABLE GroupesExamen (
  Id            INTEGER PRIMARY KEY AUTOINCREMENT,
  SessionId     INTEGER NOT NULL,
  EpreuveId     INTEGER NOT NULL DEFAULT 0,
  CodeGroupe    TEXT    NOT NULL DEFAULT '',
  EnseignantId  INTEGER NOT NULL DEFAULT 0,
  NbEleves      INTEGER NOT NULL DEFAULT 0,
  SurveillantId INTEGER,
  SalleId       INTEGER,
  TiersTemps    INTEGER NOT NULL DEFAULT 0,
  DureeMinutes  INTEGER NOT NULL DEFAULT 0,
  Type          TEXT    NOT NULL DEFAULT 'Standard',
  ClasseId      INTEGER,
  HeureFin      TEXT    NOT NULL DEFAULT '',
  PremierDepart TEXT    NOT NULL DEFAULT '',
  FOREIGN KEY (SessionId) REFERENCES Sessions(Id)
)", transaction: tx);
        await cn.ExecuteAsync(@"
INSERT INTO GroupesExamen(Id, SessionId, EpreuveId, CodeGroupe, EnseignantId,
  NbEleves, SurveillantId, SalleId, TiersTemps, DureeMinutes, Type,
  ClasseId, HeureFin, PremierDepart)
SELECT Id, SessionId, EpreuveId, CodeGroupe, EnseignantId,
  NbEleves, SurveillantId, SalleId, TiersTemps, DureeMinutes, Type,
  ClasseId, IFNULL(HeureFin,''), IFNULL(PremierDepart,'')
FROM GroupesExamen_old", transaction: tx);
        await cn.ExecuteAsync("DROP TABLE GroupesExamen_old", transaction: tx);
        await cn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_GroupesExamen_Session ON GroupesExamen(SessionId)", transaction: tx);
        await cn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_GroupesExamen_Surv    ON GroupesExamen(SurveillantId)", transaction: tx);
        await cn.ExecuteAsync("CREATE INDEX IF NOT EXISTS IDX_GroupesExamen_Ens     ON GroupesExamen(EnseignantId)", transaction: tx);
        tx.Commit();
        log?.Invoke("Migration GroupesExamen : terminée avec succès.");
    }

    // Table EpreuveGroupes (relation N-N épreuves ↔ groupes)
    await cn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS EpreuveGroupes (
  EpreuveId INTEGER NOT NULL,
  ClasseId  INTEGER NOT NULL,
  PRIMARY KEY (EpreuveId, ClasseId)
);
CREATE INDEX IF NOT EXISTS IDX_EprGrp_Epreuve ON EpreuveGroupes(EpreuveId);
CREATE INDEX IF NOT EXISTS IDX_EprGrp_Classe  ON EpreuveGroupes(ClasseId);
");

    // Colonnes défensives sur Epreuves (vieilles DB antérieures au schéma initial)
    await AddColumnIfMissing(cn, "Epreuves", "TiersTemps",    "INTEGER NOT NULL DEFAULT 0");
    await AddColumnIfMissing(cn, "Epreuves", "Ministerielle", "INTEGER NOT NULL DEFAULT 0");
    await AddColumnIfMissing(cn, "Epreuves", "Annee",         "TEXT    NOT NULL DEFAULT '2025-2026'");

    // Migration : suppression FK ClasseId→Classes dans Epreuves (inutile depuis EpreuveGroupes N-N)
    var epreuvesCreateSql = await cn.ExecuteScalarAsync<string>(
        "SELECT sql FROM sqlite_master WHERE type='table' AND name='Epreuves'");
    if (epreuvesCreateSql?.Contains("REFERENCES Classes") == true)
    {
        log?.Invoke("Migration Epreuves : suppression FK ClasseId→Classes → démarrage.");
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("ALTER TABLE Epreuves RENAME TO Epreuves_old", transaction: tx);
        await cn.ExecuteAsync(@"
CREATE TABLE Epreuves (
  Id           INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom          TEXT    NOT NULL,
  ClasseId     INTEGER NOT NULL DEFAULT 0,
  DureeMinutes INTEGER NOT NULL,
  TiersTemps   INTEGER NOT NULL DEFAULT 0,
  Ministerielle INTEGER NOT NULL DEFAULT 0,
  Annee        TEXT    NOT NULL
)", transaction: tx);
        await cn.ExecuteAsync(@"
INSERT INTO Epreuves(Id, Nom, ClasseId, DureeMinutes, TiersTemps, Ministerielle, Annee)
SELECT Id, Nom, ClasseId, DureeMinutes, TiersTemps, Ministerielle, Annee FROM Epreuves_old",
            transaction: tx);
        await cn.ExecuteAsync("DROP TABLE Epreuves_old", transaction: tx);
        tx.Commit();
        log?.Invoke("Migration Epreuves : terminée avec succès.");
    }

    // Migration Niveau sur Epreuves
    await AddColumnIfMissing(cn, "Epreuves", "Niveau", "INTEGER NOT NULL DEFAULT 0");

    // Migration RolesSurveillance : ajout plage horaire (remplace Local + DureeMinutes saisi)
    await AddColumnIfMissing(cn, "RolesSurveillance", "HeureDebut", "TEXT NOT NULL DEFAULT ''");
    await AddColumnIfMissing(cn, "RolesSurveillance", "HeureFin",   "TEXT NOT NULL DEFAULT ''");

    // Migration RolesSurveillance : Date indépendante de la session
    await AddColumnIfMissing(cn, "RolesSurveillance", "Date", "TEXT NOT NULL DEFAULT ''");
    // Backfill : renseigner Date depuis la session liée pour les anciens enregistrements
    await cn.ExecuteAsync(@"
        UPDATE RolesSurveillance
        SET Date = (SELECT s.Date FROM Sessions s WHERE s.Id = RolesSurveillance.SessionId)
        WHERE Date = '' AND SessionId > 0
          AND EXISTS (SELECT 1 FROM Sessions s WHERE s.Id = RolesSurveillance.SessionId)");

    // Migration JourCycle : Sessions
    await AddColumnIfMissing(cn, "Sessions", "JourCycle", "INTEGER NOT NULL DEFAULT 0");

    // Migration JourCycle : QuotasMinutes — recrée la table avec le nouveau UNIQUE(ProfId, JourCycle, AnneeScolaire)
    var hasJourCycleQuotas = await cn.ExecuteScalarAsync<long>(
        "SELECT COUNT(*) FROM pragma_table_info('QuotasMinutes') WHERE name='JourCycle';");
    if (hasJourCycleQuotas == 0)
    {
        log?.Invoke("Migration QuotasMinutes : ajout colonne JourCycle → démarrage.");
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
        log?.Invoke("Migration QuotasMinutes : terminée avec succès.");
    }

    // Table des zones de surveillance configurables par école
    await cn.ExecuteAsync(@"
CREATE TABLE IF NOT EXISTS ZonesSurveillance (
  Id    INTEGER PRIMARY KEY AUTOINCREMENT,
  Nom   TEXT    NOT NULL,
  Ordre INTEGER NOT NULL DEFAULT 0
);");

    // Seeding des zones par défaut si la table est vide (première installation ou migration)
    var zoneCount = await cn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ZonesSurveillance");
    if (zoneCount == 0)
    {
        log?.Invoke("Insertion des zones de surveillance par défaut.");
        await cn.ExecuteAsync(@"
INSERT INTO ZonesSurveillance(Nom, Ordre) VALUES
  ('Surveillance 1er étage', 1),
  ('Surveillance 3e étage',  2),
  ('Surveillance bibliothèque SAI', 3),
  ('Disponibilités et pauses', 4);");
    }

    log?.Invoke("Toutes les migrations terminées.");
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