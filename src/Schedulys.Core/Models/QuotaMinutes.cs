namespace Schedulys.Core.Models;

public sealed class QuotaMinutes
{
    public int    Id            { get; set; }
    public int    ProfId        { get; set; }
    public int    MinutesMax    { get; set; }
    public string AnneeScolaire { get; set; } = "2025-2026";
}
