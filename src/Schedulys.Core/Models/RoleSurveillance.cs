namespace Schedulys.Core.Models;

public sealed class RoleSurveillance
{
    public int    Id            { get; set; }
    public int    SessionId     { get; set; }
    public string Date          { get; set; } = "";
    public string TypeRole      { get; set; } = "";
    public int    SurveillantId { get; set; }
    public string HeureDebut    { get; set; } = "";
    public string HeureFin      { get; set; } = "";
    public int    DureeMinutes  { get; set; }
}
