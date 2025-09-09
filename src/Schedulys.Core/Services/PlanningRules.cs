using Schedulys.Core.Models;
using System.Globalization;

namespace Schedulys.Core.Services;
public sealed class PlanningRules : IPlanningRules
{
    private static DateTime ParseDate(string s)
        => DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static TimeSpan ParseTime(string s)
        => TimeSpan.ParseExact(s, "hh\\:mm", CultureInfo.InvariantCulture);

    private static (DateTime start, DateTime end) GetInterval(Creneau c)
    {
        // Compose full DateTime from separate Date and time strings
        var d = ParseDate(c.Date);
        var start = d.Add(ParseTime(c.HeureDebut));
        var end   = d.Add(ParseTime(c.HeureFin));
        return (start, end);
    }
    public DateTime CalcHeureFin(DateTime debut, int dureeMinutes, bool tt, PlanningSettings s)
    {
        var minutes = tt ? (int)Math.Ceiling(dureeMinutes * s.TiersTempsMultiplier) : dureeMinutes;
        return debut.AddMinutes(minutes);
    }

    public bool Chevauchent(DateTime d1, DateTime f1, DateTime d2, DateTime f2)
        => d1 < f2 && d2 < f1;

    public bool ProfEnConflit(int profId, IEnumerable<Creneau> xs, Creneau cand)
    {
        var (cStart, cEnd) = GetInterval(cand);
        return xs.Any(c => c.SurveillantId == profId && c.Date == cand.Date &&
                           Chevauchent(GetInterval(c).start, GetInterval(c).end, cStart, cEnd));
    }

    public bool SalleEnConflit(int salleId, IEnumerable<Creneau> xs, Creneau cand)
    {
        var (cStart, cEnd) = GetInterval(cand);
        return xs.Any(c => c.SalleId == salleId && c.Date == cand.Date &&
                           Chevauchent(GetInterval(c).start, GetInterval(c).end, cStart, cEnd));
    }

    public bool CapaciteValide(int capaciteSalle, int nbEleves) => nbEleves <= capaciteSalle;
}