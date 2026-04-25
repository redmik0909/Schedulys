namespace Schedulys.Core.Models;

public sealed class RoleSurveillance
{
    public int     Id             { get; set; }
    public int     SessionId      { get; set; }
    public string  TypeRole       { get; set; } = ""; // "1er étage","3e étage","Bibliothèque SAI","Disponibilités"
    public int     SurveillantId  { get; set; }
    public string? Local          { get; set; }
    public int     DureeMinutes   { get; set; }
}
