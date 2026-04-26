namespace Schedulys.Core.Models;

public sealed class Session
{
    public int    Id            { get; set; }
    public string Date          { get; set; } = "";   // yyyy-MM-dd
    public string Periode       { get; set; } = "AM"; // "AM" ou "PM"
    public string HeureDebut    { get; set; } = "";   // ex: "08:30"
    public string AnneeScolaire { get; set; } = "2025-2026";
    public int    JourCycle     { get; set; }         // 0 = non défini, 1–9
}
