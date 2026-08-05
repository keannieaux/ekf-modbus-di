using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Services;

namespace ShnoSetting.Core.ViewModels;

public partial class StarterViewModel : ObservableObject
{
    /// <summary>Номер пускателя 1..4 (КМ1 = 1).</summary>
    public int Number { get; init; }

    /// <summary>Обратная связь (из опроса).</summary>
    [ObservableProperty] private bool _feedbackOn;

    /// <summary>Бит ручного включения (уходит в маску управления).</summary>
    [ObservableProperty] private bool _manualOn;
}

/// <summary>Управление пускателями: общая маска + общая длительность ручного режима.</summary>
public partial class StartersViewModel : ObservableObject
{
    private StartersService? _service;

    public ObservableCollection<StarterViewModel> Starters { get; } = new();

    /// <summary>Длительность ручного режима, секунды (0 = выключить).</summary>
    [ObservableProperty] private int _durationSec;

    [ObservableProperty] private string _status = "";

    public StartersViewModel(int count = 4)
    {
        for (int i = 1; i <= count; i++)
            Starters.Add(new StarterViewModel { Number = i });
    }

    internal void Attach(StartersService service) => _service = service;

    internal void ApplyFeedback(bool[] feedback)
    {
        int n = Math.Min(Starters.Count, feedback.Length);
        for (int i = 0; i < n; i++)
            Starters[i].FeedbackOn = feedback[i];
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        try
        {
            int mask = 0;
            for (int i = 0; i < Starters.Count; i++)
                if (Starters[i].ManualOn) mask |= 1 << i;

            await _service.WriteMaskAsync(mask);
            await _service.WriteDurationAsync(DurationSec);
            Status = DurationSec > 0
                ? $"Ручной режим: маска {mask}, {DurationSec} с"
                : "Ручной режим выключен";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
    }
}
