namespace Schedulys.Core.Models;
public sealed class Salle
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public int Capacite { get; set; }
    public string? Type { get; set; }
    public string Annee { get; set; } = "2025-2026";
}