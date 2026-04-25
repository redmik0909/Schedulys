using System.Windows;
using Schedulys.App.ViewModels;

namespace Schedulys.App.Views;

public partial class MainWindow : Window
{
    private void OnPlanningVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && (sender as PlanningView)?.DataContext is PlanningViewModel vm)
            vm.RefreshCommand.Execute(null);
    }

    private void OnExamsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue && (sender as ExamsView)?.DataContext is ExamsViewModel vm)
            vm.RefreshCommand.Execute(null);
    }
}
