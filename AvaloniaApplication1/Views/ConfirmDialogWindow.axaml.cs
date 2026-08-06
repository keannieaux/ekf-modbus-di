using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvaloniaApplication1.Views;

/// <summary>Простой модальный диалог подтверждения (Да/Отмена).</summary>
public partial class ConfirmDialogWindow : Window
{
    public ConfirmDialogWindow()
    {
        InitializeComponent();
    }

    public ConfirmDialogWindow(string message) : this()
    {
        MessageText.Text = message;
    }

    private void OnYes(object? sender, RoutedEventArgs e) => Close(true);
    private void OnNo(object? sender, RoutedEventArgs e) => Close(false);
}
