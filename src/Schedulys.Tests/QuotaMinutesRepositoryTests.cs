using Schedulys.Core.Models;
using Schedulys.Tests.Helpers;

namespace Schedulys.Tests;

public sealed class QuotaMinutesRepositoryTests : IAsyncLifetime
{
    private TestDb _tdb = null!;

    public async Task InitializeAsync() => _tdb = await TestDb.CreateAsync();
    public async Task DisposeAsync()    => await _tdb.DisposeAsync();

    private async Task<int> MakeProfAsync(string nom = "Alice")
        => await _tdb.Db.Profs.CreateAsync(new Prof { Nom = nom, Annee = "2025-2026" });

    // ── CreateAsync + GetByProfAsync ──────────────────────────────────────────

    [Fact]
    public async Task Create_And_GetByProf_RoundTrip()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();

        var id  = await db.Quotas.CreateAsync(new QuotaMinutes
            { ProfId = profId, JourCycle = 0, MinutesMax = 120, AnneeScolaire = "2025-2026" });
        var got = await db.Quotas.GetByProfAsync(profId, jourCycle: 0, annee: "2025-2026");

        Assert.NotNull(got);
        Assert.Equal(profId,      got.ProfId);
        Assert.Equal(0,           got.JourCycle);
        Assert.Equal(120,         got.MinutesMax);
        Assert.Equal("2025-2026", got.AnneeScolaire);
    }

    [Fact]
    public async Task GetByProf_NonExistent_ReturnsNull()
    {
        var got = await _tdb.Db.Quotas.GetByProfAsync(9999, jourCycle: 0);
        Assert.Null(got);
    }

    [Fact]
    public async Task Create_ReturnsPositiveId()
    {
        var profId = await MakeProfAsync();
        var id     = await _tdb.Db.Quotas.CreateAsync(new QuotaMinutes
            { ProfId = profId, MinutesMax = 60, AnneeScolaire = "2025-2026" });
        Assert.True(id > 0);
    }

    // ── GetAllByProfAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllByProf_ReturnsMultipleJourCycle()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();

        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 0, MinutesMax = 90,  AnneeScolaire = "2025-2026" });
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 1, MinutesMax = 120, AnneeScolaire = "2025-2026" });
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 2, MinutesMax = 60,  AnneeScolaire = "2025-2026" });

        var all = await db.Quotas.GetAllByProfAsync(profId);
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetAllByProf_OrderedByJourCycle()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 3, MinutesMax = 60,  AnneeScolaire = "2025-2026" });
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 1, MinutesMax = 120, AnneeScolaire = "2025-2026" });

        var all = await db.Quotas.GetAllByProfAsync(profId);
        Assert.Equal(1, all[0].JourCycle);
        Assert.Equal(3, all[1].JourCycle);
    }

    [Fact]
    public async Task GetAllByProf_WithAnnee_FiltersOnYear()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 0, MinutesMax = 90, AnneeScolaire = "2025-2026" });
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 0, MinutesMax = 60, AnneeScolaire = "2026-2027" });

        var result = await db.Quotas.GetAllByProfAsync(profId, annee: "2025-2026");
        Assert.Single(result);
        Assert.Equal(90, result[0].MinutesMax);
    }

    [Fact]
    public async Task GetAllByProf_Empty_WhenNoneExist()
    {
        var all = await _tdb.Db.Quotas.GetAllByProfAsync(9999);
        Assert.Empty(all);
    }

    // ── UpsertAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_CreatesNew_WhenNotExists()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();

        var ok = await db.Quotas.UpsertAsync(new QuotaMinutes
            { ProfId = profId, JourCycle = 0, MinutesMax = 100, AnneeScolaire = "2025-2026" });
        Assert.True(ok);

        var got = await db.Quotas.GetByProfAsync(profId, jourCycle: 0, annee: "2025-2026");
        Assert.NotNull(got);
        Assert.Equal(100, got.MinutesMax);
    }

    [Fact]
    public async Task Upsert_UpdatesExisting_OnConflict()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();

        await db.Quotas.UpsertAsync(new QuotaMinutes
            { ProfId = profId, JourCycle = 0, MinutesMax = 100, AnneeScolaire = "2025-2026" });
        // Deuxième upsert → doit mettre à jour
        await db.Quotas.UpsertAsync(new QuotaMinutes
            { ProfId = profId, JourCycle = 0, MinutesMax = 200, AnneeScolaire = "2025-2026" });

        var got = await db.Quotas.GetByProfAsync(profId, jourCycle: 0, annee: "2025-2026");
        Assert.Equal(200, got!.MinutesMax); // doit refléter la nouvelle valeur
    }

    [Fact]
    public async Task Upsert_DifferentJourCycle_CreatesSeperateRows()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();

        await db.Quotas.UpsertAsync(new QuotaMinutes
            { ProfId = profId, JourCycle = 1, MinutesMax = 90, AnneeScolaire = "2025-2026" });
        await db.Quotas.UpsertAsync(new QuotaMinutes
            { ProfId = profId, JourCycle = 2, MinutesMax = 60, AnneeScolaire = "2025-2026" });

        var all = await db.Quotas.GetAllByProfAsync(profId);
        Assert.Equal(2, all.Count);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesQuota()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();
        var id     = await db.Quotas.CreateAsync(new QuotaMinutes
            { ProfId = profId, MinutesMax = 60, AnneeScolaire = "2025-2026" });

        var ok = await db.Quotas.DeleteAsync(id);
        Assert.True(ok);

        var got = await db.Quotas.GetByProfAsync(profId);
        Assert.Null(got);
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsFalse()
    {
        Assert.False(await _tdb.Db.Quotas.DeleteAsync(9999));
    }

    // ── ListAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsAllQuotas()
    {
        var db     = _tdb.Db;
        var prof1  = await MakeProfAsync("Alice");
        var prof2  = await MakeProfAsync("Bob");
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = prof1, MinutesMax = 90, AnneeScolaire = "2025-2026" });
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = prof2, MinutesMax = 60, AnneeScolaire = "2025-2026" });

        var all = await db.Quotas.ListAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task List_WithAnnee_FiltersCorrectly()
    {
        var db     = _tdb.Db;
        var profId = await MakeProfAsync();
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 0, MinutesMax = 90, AnneeScolaire = "2025-2026" });
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, JourCycle = 1, MinutesMax = 60, AnneeScolaire = "2026-2027" });

        var result = await db.Quotas.ListAsync(annee: "2025-2026");
        Assert.Single(result);
        Assert.Equal("2025-2026", result[0].AnneeScolaire);
    }
}
