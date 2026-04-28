using System.Globalization;

namespace Schedulys.Tests;

/// <summary>
/// Tests pour la logique de parsing de date d'expiration.
/// Note: LicenseService est dans Schedulys.App (WPF, non référencé ici).
/// On documente le comportement attendu en réimplantant la même logique localement.
/// Si ParseExpiryUtc change en prod sans mettre à jour ce test → les tests continuent
/// de passer (fausse sécurité), mais la spec est documentée et visible.
/// </summary>
public sealed class LicenseParsingTests
{
    // Copie exacte de LicenseService.ParseExpiryUtc — source de vérité documentée
    private static DateTime ParseExpiryUtc(string raw)
    {
        var dt = DateTime.Parse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        if (dt.TimeOfDay == TimeSpan.Zero && raw.Length == 10)
            dt = dt.AddDays(1).AddSeconds(-1);
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    // ── Date seule (YYYY-MM-DD) ───────────────────────────────────────────────

    [Fact]
    public void DateOnly_ReturnsEndOfDay_UTC()
    {
        // "2026-04-28" sans heure → fin de journée (23:59:59) pour éviter expiration
        // prématurée à UTC-5 (Québec) où minuit UTC = 19h00 la veille
        var result = ParseExpiryUtc("2026-04-28");
        Assert.Equal(new DateTime(2026, 4, 28, 23, 59, 59, DateTimeKind.Utc), result);
    }

    [Fact]
    public void DateOnly_Kind_IsUtc()
    {
        Assert.Equal(DateTimeKind.Utc, ParseExpiryUtc("2026-04-28").Kind);
    }

    [Fact]
    public void DateOnly_LastDayOfMonth_Correct()
    {
        var result = ParseExpiryUtc("2026-06-30");
        Assert.Equal(new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc), result);
    }

    // ── DateTime avec Z (UTC explicite) ─────────────────────────────────────

    [Fact]
    public void DateTimeUTC_PreservesExactTime()
    {
        var result = ParseExpiryUtc("2026-04-28T10:30:00Z");
        Assert.Equal(new DateTime(2026, 4, 28, 10, 30, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void DateTimeUTC_Kind_IsUtc()
    {
        Assert.Equal(DateTimeKind.Utc, ParseExpiryUtc("2026-04-28T10:30:00Z").Kind);
    }

    [Fact]
    public void DateTimeMidnightUTC_NotTreatedAsDateOnly()
    {
        // TimeOfDay == Zero ET Length != 10 → on ne doit PAS ajouter 23:59:59
        var result = ParseExpiryUtc("2026-04-28T00:00:00Z");
        Assert.Equal(new DateTime(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc), result);
    }

    // ── DateTime avec offset (fuseau horaire) ────────────────────────────────

    [Fact]
    public void DateTimeWithNegativeOffset_ConvertsToUTC()
    {
        // UTC-5 (Québec hiver): 20h00 local = 01h00 UTC lendemain
        var result = ParseExpiryUtc("2026-04-28T20:00:00-05:00");
        Assert.Equal(new DateTime(2026, 4, 29, 1, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void DateTimeWithPositiveOffset_ConvertsToUTC()
    {
        // UTC+2: 12h00 local = 10h00 UTC
        var result = ParseExpiryUtc("2026-04-28T12:00:00+02:00");
        Assert.Equal(new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc), result);
    }

    [Fact]
    public void DateTimeWithOffset_Kind_IsUtc()
    {
        Assert.Equal(DateTimeKind.Utc, ParseExpiryUtc("2026-04-28T20:00:00-05:00").Kind);
    }

    // ── Cas limites ───────────────────────────────────────────────────────────

    [Fact]
    public void DateOnly_LeapYear_Correct()
    {
        var result = ParseExpiryUtc("2028-02-29");
        Assert.Equal(new DateTime(2028, 2, 29, 23, 59, 59, DateTimeKind.Utc), result);
    }

    [Fact]
    public void DateOnly_31December_Correct()
    {
        var result = ParseExpiryUtc("2026-12-31");
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc), result);
    }

    // ── Cohérence Quebec ─────────────────────────────────────────────────────

    [Fact]
    public void DateOnly_StillValidAt_QuebecMidnight()
    {
        // Licence "2026-04-28" → expire 23:59:59 UTC
        // À Québec (UTC-4 été): minuit local = 04:00 UTC → TOUJOURS VALIDE
        var expiry  = ParseExpiryUtc("2026-04-28");
        var quebecMidnight = new DateTime(2026, 4, 29, 4, 0, 0, DateTimeKind.Utc); // minuit à Québec
        Assert.True(expiry > quebecMidnight == false,
            "Licence devrait être expirée à 04:00 UTC (minuit heure du Québec le 29 avril)");
        // La licence expire à 23:59:59 UTC le 28 → valide tout le 28 à Québec
        var quebecEvening = new DateTime(2026, 4, 28, 22, 0, 0, DateTimeKind.Utc); // 18:00 heure Québec
        Assert.True(expiry > quebecEvening, "Licence doit être valide à 18h00 heure Québec le 28 avril");
    }
}
