using System.Windows;
using Schedulys.App.ViewModels;

namespace Schedulys.App.Views;

public partial class ActivationWindow : Window
{
    public LicenseInfo? Result { get; private set; }

    public ActivationWindow()
    {
        InitializeComponent();
        var vm = new ActivationViewModel();
        vm.ActivationSucceeded += info =>
        {
            Result = info;
            DialogResult = true;
            Close();
        };
        DataContext = vm;
    }

    private void DemanderLicence_Click(object sender, RoutedEventArgs e)
        => new RequestLicenseWindow { Owner = this }.ShowDialog();
}
