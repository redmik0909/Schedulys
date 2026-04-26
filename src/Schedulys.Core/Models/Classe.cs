namespace Schedulys.Core.Models;

public sealed class Classe
{
    public int    Id          { get; set; }
    public string Code        { get; set; } = ""; // ex: "132108-01"
    public string Description { get; set; } = ""; // ex: "français écriture"
    public int    ProfId      { get; set; }
    public int    Effectif    { get; set; }
    public string Annee       { get; set; } = "2025-2026";

    // Legacy (conservés en BD, non utilisés dans la nouvelle UI)
    public string Nom    { get; set; } = "";
    public int    Niveau { get; set; }

    // Rempli après JOIN — non persisté
    public string NomProf { get; set; } = "";

    public string Label => string.IsNullOrWhiteSpace(Code)
        ? Nom
        : $"{Code} — {Description}";
}
