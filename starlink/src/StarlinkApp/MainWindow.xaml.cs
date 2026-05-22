using System.Windows;
using System.Windows.Threading;
using StarlinkApp.Services;
using StarlinkApp.Simulation;
using StarlinkApp.ViewModels;

namespace StarlinkApp;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(
            new InProcessSimulatorClient(),
            FileLogService.CreateDefault());

        DataContext = _viewModel;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => _viewModel.RefreshSnapshot();
        _timer.Start();
    }
}
