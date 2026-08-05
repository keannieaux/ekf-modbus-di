using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvaloniaApplication1.Views;
using ShnoSetting.Core.Profiles;
using ShnoSetting.Core.Settings;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1;

public partial class App : Application
{
    private MainViewModel? _mainViewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Composition root: весь сервисный слой живёт в ShnoSetting.Core.
            string baseDir = AppContext.BaseDirectory;
            var settingsStore = new SettingsStore(Path.Combine(baseDir, "settings.json"));
            var profileProvider = new ProfileProvider(Path.Combine(baseDir, "profiles"));

            _mainViewModel = new MainViewModel(settingsStore.Load(), settingsStore, profileProvider);
            desktop.MainWindow = new MainWindow { DataContext = _mainViewModel };
            desktop.Exit += (_, _) => _mainViewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
