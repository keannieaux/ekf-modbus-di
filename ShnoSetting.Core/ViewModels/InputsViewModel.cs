using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Services;

namespace ShnoSetting.Core.ViewModels;

public partial class DiscreteInputViewModel : ObservableObject
{
    public int Index { get; init; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private bool _rawValue;
    [ObservableProperty] private bool _outputValue;
    [ObservableProperty] private int _selector;
    [ObservableProperty] private bool _isNc;
}

/// <summary>Дискретные входы (64 шт.): состояния из опроса, конфигурация по кнопкам.</summary>
public partial class InputsViewModel : ObservableObject
{
    private DiscreteInputsService? _service;

    public ObservableCollection<DiscreteInputViewModel> Inputs { get; } = new();

    [ObservableProperty] private string _status = "";

    public InputsViewModel(IEnumerable<string> names)
    {
        int i = 0;
        foreach (var name in names)
            Inputs.Add(new DiscreteInputViewModel { Index = i++, Name = name });
    }

    internal void Attach(DiscreteInputsService service) => _service = service;

    /// <summary>Обновление мгновенных значений из циклического опроса (UI-поток).</summary>
    internal void ApplyStates(InputStates states)
    {
        int n = Math.Min(Inputs.Count, states.Raw.Length);
        for (int i = 0; i < n; i++)
        {
            Inputs[i].RawValue = states.Raw[i];
            Inputs[i].OutputValue = states.Output[i];
        }
    }

    public string[] GetNames() => Inputs.Select(x => x.Name).ToArray();

    [RelayCommand]
    private async Task ReadConfigAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        try
        {
            var config = await _service.ReadConfigAsync();
            int n = Math.Min(Inputs.Count, config.Selectors.Length);
            for (int i = 0; i < n; i++)
            {
                Inputs[i].Selector = config.Selectors[i];
                Inputs[i].IsNc = config.NoNc[i];
            }
            Status = $"Прочитана конфигурация {n} входов";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
    }

    [RelayCommand]
    private async Task WriteConfigAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        try
        {
            var config = new InputConfig(
                Inputs.Select(x => x.Selector).ToArray(),
                Inputs.Select(x => x.IsNc).ToArray());
            await _service.WriteConfigAsync(config);
            Status = $"Записана конфигурация {Inputs.Count} входов";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
    }
}
