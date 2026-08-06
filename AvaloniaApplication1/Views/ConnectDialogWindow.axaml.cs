using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

/// <summary>
/// Модальное окно подключения к ПЛК: профиль, IP, порт.
/// Показывается при старте приложения и при потере связи.
/// Закрывается автоматически, когда подключение установлено
/// (в том числе после автоматического переподключения).
/// </summary>
public partial class ConnectDialogWindow : Window
{
    private readonly MainViewModel? _viewModel;

    public ConnectDialogWindow()
    {
        InitializeComponent();
    }

    /// <param name="viewModel">Общая корневая ViewModel с параметрами подключения.</param>
    /// <param name="message">Пояснение в шапке: первый запуск или потеря связи.</param>
    public ConnectDialogWindow(MainViewModel viewModel, string message)
        : this()
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        MessageText.Text = message;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsConnected)
            && _viewModel is { IsConnected: true })
            Close();
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
