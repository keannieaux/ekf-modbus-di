using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class StartersView : UserControl
{
    public StartersView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => LoadDurationFromViewModel();
    }

    private StartersViewModel? ViewModel => DataContext as StartersViewModel;

    /// <summary>
    /// ViewModel хранит длительность одним числом в секундах, а макет требует три поля.
    /// Пересчёт — чисто представление, поэтому живёт здесь, а не в Core.
    /// </summary>
    private void OnDurationChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (ViewModel is null)
            return;

        ViewModel.DurationSec =
            (int)(Hours.Value ?? 0) * 3600 +
            (int)(Minutes.Value ?? 0) * 60 +
            (int)(Seconds.Value ?? 0);
    }

    private void LoadDurationFromViewModel()
    {
        int total = Math.Max(0, ViewModel?.DurationSec ?? 0);

        Hours.Value = total / 3600;
        Minutes.Value = total % 3600 / 60;
        Seconds.Value = total % 60;
    }

    /// <summary>
    /// «Выключить все»: снять все биты маски и обнулить длительность, затем применить.
    /// Отдельной команды для этого в ViewModel нет.
    /// </summary>
    private void OnTurnOffAll(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        foreach (StarterViewModel starter in ViewModel.Starters)
            starter.ManualOn = false;

        Hours.Value = 0;
        Minutes.Value = 0;
        Seconds.Value = 0;
        ViewModel.DurationSec = 0;

        if (ViewModel.ApplyCommand.CanExecute(null))
            ViewModel.ApplyCommand.Execute(null);
    }
}
