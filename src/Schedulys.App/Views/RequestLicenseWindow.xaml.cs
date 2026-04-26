using System.Windows;
using Schedulys.App.ViewModels;

namespace Schedulys.App.Views;

public partial class RequestLicenseWindow : Window
{
    public RequestLicenseWindow()
    {
        InitializeComponent();
        DataContext = new RequestLicenseViewModel();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
