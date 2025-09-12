using Avalonia.Controls;
using Schedulys.App.ViewModels;

namespace Schedulys.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainShellViewModel();
    }
}