using Avalonia.Controls;
using Avalonia.Interactivity;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class StartersView : UserControl
{
    private StartersControlWindow? _controlWindow;

    public StartersView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// «Управление пускателями…» — неблокирующее окно ручного управления.
    /// Повторное нажатие активирует уже открытое окно.
    /// </summary>
    private void OnOpenControl(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StartersViewModel viewModel)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        if (_controlWindow is not null)
        {
            _controlWindow.Activate();
            return;
        }

        _controlWindow = new StartersControlWindow { DataContext = viewModel };
        _controlWindow.Closed += (_, _) => _controlWindow = null;
        _controlWindow.Show(owner);
    }
}
