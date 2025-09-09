namespace Schedulys.Core.Models;

public sealed class Eleve
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public int ClasseId { get; set; }
    public bool TiersTemps { get; set; } = false;
    public string Annee { get; set; } = "2025-2026";
}