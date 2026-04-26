namespace Schedulys.Core.Models;

public sealed class QuotaMinutes
{
    public int    Id            { get; set; }
    public int    ProfId        { get; set; }
    public int    JourCycle     { get; set; }         // 0 = tous les jours / moyenne, 1–9
    public int    MinutesMax    { get; set; }
    public string AnneeScolaire { get; set; } = "2025-2026";
}
