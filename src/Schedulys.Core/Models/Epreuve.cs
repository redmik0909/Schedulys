namespace Schedulys.Core.Models;
public sealed class Epreuve
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public int ClasseId { get; set; }
    public int DureeMinutes { get; set; }
    public bool TiersTemps { get; set; }
    public bool Ministerielle { get; set; }
    public int  Niveau { get; set; }
    public string Annee { get; set; } = "2025-2026";
}