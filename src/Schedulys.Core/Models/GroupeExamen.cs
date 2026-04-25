namespace Schedulys.Core.Models;

public sealed class GroupeExamen
{
    public int     Id            { get; set; }
    public int     SessionId     { get; set; }
    public int     EpreuveId     { get; set; }
    public string  CodeGroupe    { get; set; } = "";         // ex: "132506-01"
    public int     EnseignantId  { get; set; }               // prof responsable du cours
    public int     NbEleves      { get; set; }
    public int?    SurveillantId { get; set; }
    public int?    SalleId       { get; set; }
    public bool    TiersTemps    { get; set; }
    public int     DureeMinutes  { get; set; }               // durée effective (tiers-temps inclus)
    public string  Type          { get; set; } = "Standard"; // "Standard", "SAI", "EHDAA"
}
