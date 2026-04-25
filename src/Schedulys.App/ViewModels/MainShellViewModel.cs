using System;
using System.Globalization;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    public TeachersViewModel Teachers { get; }
    public LocationsViewModel Locations { get; }
    public GroupesViewModel Groupes { get; }
    public ExamsViewModel Exams { get; }
    public PlanningViewModel Planning { get; }

    public string TodayLabel =>
        DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("fr-CA"));

    public MainShellViewModel(DataContext db)
    {
        Teachers  = new TeachersViewModel(db);
        Locations = new LocationsViewModel(db);
        Groupes   = new GroupesViewModel(db);
        Exams     = new ExamsViewModel(db);
        Planning  = new PlanningViewModel(db);
    }
}
