using System;
using System.Globalization;

namespace Schedulys.App.ViewModels
{
    public sealed class MainShellViewModel : ViewModelBase
    {
        // Onglets / sous-vues
        public TeachersViewModel Teachers { get; } = new();
        public LocationsViewModel Locations { get; } = new();
        public GroupesViewModel Groupes { get; } = new();
        public ExamsViewModel Exams { get; } = new();
        public PlanningViewModel Planning { get; } = new();

        // App bar (date du jour)
        public string TodayLabel =>
            DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("fr-CA"));
    }
}