using System;
using System.Windows.Input;

namespace Schedulys.App.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private string _greeting = "Bienvenue sur Schedulys!";
        public string Greeting
        {
            get => _greeting;
            set => SetProperty(ref _greeting, value);
        }

        public ICommand AjouterSalleCommand { get; }

        public MainWindowViewModel()
        {
            AjouterSalleCommand = new RelayCommand(() =>
            {
                Greeting = "Salle ajoutée (exemple)";
            });
        }

        private sealed class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool>? _canExecute;

            public RelayCommand(Action execute, Func<bool>? canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

            public void Execute(object? parameter) => _execute();

            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}