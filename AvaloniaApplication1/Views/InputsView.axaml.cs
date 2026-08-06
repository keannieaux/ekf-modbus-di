using Avalonia.Controls;
using Avalonia.Interactivity;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class InputsView : UserControl
{
    public InputsView()
    {
        InitializeComponent();
    }

    /// <summary>Сброс дискретных входов — только после подтверждения.</summary>
    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InputsViewModel vm)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var dialog = new ConfirmDialogWindow(
            "Сбросить конфигурацию дискретных входов в ПЛК?");
        bool confirmed = await dialog.ShowDialog<bool>(owner);
        if (confirmed)
            await vm.ResetCommand.ExecuteAsync(null);
    }
}
