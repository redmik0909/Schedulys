using Schedulys.Core.Models;

namespace Schedulys.Core.Services;
public interface IPlanningRules
{
    DateTime CalcHeureFin(DateTime debut, int dureeMinutes, bool tiersTemps, PlanningSettings s);
    bool Chevauchent(DateTime d1, DateTime f1, DateTime d2, DateTime f2);
    bool ProfEnConflit(int profId, IEnumerable<Creneau> existants, Creneau candidat);
    bool SalleEnConflit(int salleId, IEnumerable<Creneau> existants, Creneau candidat);
    bool CapaciteValide(int capaciteSalle, int nbEleves);
}