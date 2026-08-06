using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class MainWindow : Window
{
    private bool _connectDialogOpen;
    private bool _wasConnected;

    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (ViewModel is { } viewModel)
            viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // При первом запуске сразу просим подключиться.
        ShowConnectDialog(
            "Сначала подключитесь к ПЛК: выберите профиль, укажите IP и порт, "
            + "затем нажмите «Подключиться».");
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ViewModel is { } viewModel)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsConnected) || ViewModel is not { } viewModel)
            return;

        if (viewModel.IsConnected)
        {
            _wasConnected = true;
            return;
        }

        // Обрыв после успешного подключения — показываем окно снова.
        // Диспетчер сам переподключается, при успехе окно закроется автоматически.
        if (_wasConnected)
            ShowConnectDialog(
                "Связь с ПЛК потеряна. Идёт автоматическое переподключение — "
                + "либо проверьте параметры и подключитесь вручную.");
    }

    private void OnOpenConnectDialog(object? sender, RoutedEventArgs e)
        => ShowConnectDialog(
            "Выберите профиль, укажите IP и порт, затем нажмите «Подключиться».");

    private async void ShowConnectDialog(string message)
    {
        if (_connectDialogOpen || ViewModel is not { } viewModel)
            return;

        _connectDialogOpen = true;
        try
        {
            await new ConnectDialogWindow(viewModel, message).ShowDialog(this);
        }
        finally
        {
            _connectDialogOpen = false;
        }
    }
}
