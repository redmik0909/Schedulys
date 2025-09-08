namespace Schedulys.Core.Models;
public sealed class Creneau
{
    public int Id { get; set; }
    public int EpreuveId { get; set; }
    public int SalleId { get; set; }
    public int SurveillantId { get; set; }
    public string Date { get; set; } = "";       // yyyy-MM-dd
    public string HeureDebut { get; set; } = ""; // HH:mm
    public string HeureFin { get; set; } = "";   // HH:mm
    public string Statut { get; set; } = "brouillon";
}