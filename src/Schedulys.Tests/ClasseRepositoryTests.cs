using Schedulys.Core.Models;
using Schedulys.Tests.Helpers;

namespace Schedulys.Tests;

public sealed class ClasseRepositoryTests : IAsyncLifetime
{
    private TestDb _tdb = null!;

    public async Task InitializeAsync() => _tdb = await TestDb.CreateAsync();
    public async Task DisposeAsync()    => await _tdb.DisposeAsync();

    private static Classe MakeClasse(string code = "132506", string desc = "Mathématiques") => new()
    {
        Code        = code,
        Description = desc,
        Nom         = code,
        Effectif    = 25,
        Annee       = "2025-2026",
    };

    // ── CRUD de base ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_And_GetById_RoundTrip()
    {
        var db      = _tdb.Db;
        var id      = await db.Classes.CreateAsync(MakeClasse());
        var got     = await db.Classes.GetAsync(id);

        Assert.NotNull(got);
        Assert.Equal(id,             got.Id);
        Assert.Equal("132506",       got.Code);
        Assert.Equal("Mathématiques", got.Description);
        Assert.Equal(25,             got.Effectif);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        Assert.Null(await _tdb.Db.Classes.GetAsync(9999));
    }

    [Fact]
    public async Task List_WithSearch_MatchesCode()
    {
        var db = _tdb.Db;
        await db.Classes.CreateAsync(MakeClasse("132506", "Maths"));
        await db.Classes.CreateAsync(MakeClasse("242010", "Français"));

        var result = await db.Classes.ListAsync(search: "1325");
        Assert.Single(result);
        Assert.Equal("132506", result[0].Code);
    }

    [Fact]
    public async Task List_WithSearch_MatchesDescription()
    {
        var db = _tdb.Db;
        await db.Classes.CreateAsync(MakeClasse("132506", "Mathématiques avancées"));
        await db.Classes.CreateAsync(MakeClasse("242010", "Français"));

        var result = await db.Classes.ListAsync(search: "avancées");
        Assert.Single(result);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var db = _tdb.Db;
        var id = await db.Classes.CreateAsync(MakeClasse("OLD", "Ancien"));

        await db.Classes.UpdateAsync(new Classe
        {
            Id          = id,
            Code        = "NEW",
            Description = "Nouveau",
            Nom         = "NEW",
            Effectif    = 30,
            Annee       = "2025-2026",
        });

        var got = await db.Classes.GetAsync(id);
        Assert.Equal("NEW",     got!.Code);
        Assert.Equal("Nouveau", got.Description);
        Assert.Equal(30,        got.Effectif);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesClasse()
    {
        var db = _tdb.Db;
        var id = await db.Classes.CreateAsync(MakeClasse());
        await db.Classes.DeleteAsync(id);
        Assert.Null(await db.Classes.GetAsync(id));
    }

    [Fact]
    public async Task Delete_CascadesEpreuves()
    {
        var db       = _tdb.Db;
        var classeId = await db.Classes.CreateAsync(MakeClasse());
        await db.Epreuves.CreateAsync(new Epreuve
            { Nom = "Math", ClasseId = classeId, DureeMinutes = 90, Annee = "2025-2026" });

        await db.Classes.DeleteAsync(classeId);

        var epreuves = await db.Epreuves.ListAsync(annee: "2025-2026");
        Assert.Empty(epreuves);
    }

    [Fact(DisplayName = "BUG-004: ClasseRepository.DeleteAsync ne nettoie pas EpreuveGroupes")]
    public async Task Delete_Bug004_DoesNotCleanEpreuveGroupes()
    {
        // Arrange: classe avec une entrée dans EpreuveGroupes (N-N épreuves↔groupes)
        var db        = _tdb.Db;
        var classeId  = await db.Classes.CreateAsync(MakeClasse("132506", "Maths"));
        var epreuveId = await db.Epreuves.CreateAsync(new Epreuve
            { Nom = "Math", ClasseId = classeId, DureeMinutes = 90, Annee = "2025-2026" });

        // Insérer directement dans EpreuveGroupes (pas d'API repo → SQL direct via Dapper)
        // On simule ce que ferait l'UI quand elle lie une épreuve à un groupe
        using var cn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_tdb.Path};Foreign Keys=False");
        await cn.OpenAsync();
        await Dapper.SqlMapper.ExecuteAsync(cn,
            "INSERT INTO EpreuveGroupes(EpreuveId, ClasseId) VALUES (@epreuveId, @classeId)",
            new { epreuveId, classeId });

        // Act
        await db.Classes.DeleteAsync(classeId);

        // Assert: EpreuveGroupes devrait être vide — mais ce test va ÉCHOUER (bug!)
        var count = await Dapper.SqlMapper.ExecuteScalarAsync<long>(cn,
            "SELECT COUNT(*) FROM EpreuveGroupes WHERE ClasseId=@classeId", new { classeId });
        Assert.Equal(0, count);
    }
}
