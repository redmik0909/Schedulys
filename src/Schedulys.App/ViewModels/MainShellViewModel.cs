namespace Schedulys.App.ViewModels;

public sealed class MainShellViewModel : ViewModelBase
{
    public TeachersViewModel Teachers { get; } = new();
    public LocationsViewModel Locations { get; } = new();
    public GroupesViewModel Classes { get; } = new();
    public ExamsViewModel Exams { get; } = new();
    public PlanningViewModel Planning { get; } = new();
    public ExportsViewModel Exports { get; } = new();
}