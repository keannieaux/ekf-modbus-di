using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Services;
using ShnoSetting.Core.Settings;

namespace ShnoSetting.Core.ViewModels;

public partial class DiscreteInputViewModel : ObservableObject
{
    /// <summary>Маркер «вход не используется» в списке выбора (совпадает с профилем по умолчанию).</summary>
    public const int UnusedSelectorValue = 100;

    private static readonly int[] SelectorValuesSource =
        Enumerable.Range(0, AppSettings.InputCount + 1).Append(UnusedSelectorValue).ToArray();

    public int Index { get; init; }

    /// <summary>Фиксированное наименование входа (не редактируется).</summary>
    public string Name { get; init; } = "";

    /// <summary>Значения выпадающего списка назначения: 0..число входов + маркер «не используется».</summary>
    public IReadOnlyList<int> SelectorValues => SelectorValuesSource;

    [ObservableProperty] private bool _rawValue;
    [ObservableProperty] private bool _outputValue;
    [ObservableProperty] private int _selector;
    [ObservableProperty] private string _selectorText = "0";
    [ObservableProperty] private bool _isNc;

    // Ручной ввод в редактируемом ComboBox: текст → число.
    partial void OnSelectorTextChanged(string value)
    {
        if (int.TryParse(value, out int v) && v != Selector)
            Selector = v;
    }

    // Изменение из опроса/списка: число → текст.
    partial void OnSelectorChanged(int value)
    {
        var s = value.ToString();
        if (SelectorText != s)
            SelectorText = s;
    }
}

/// <summary>
/// Дискретные входы (64 шт.): состояния и конфигурация читаются циклическим опросом,
/// изменения назначения/НО-НЗ записываются в ПЛК сразу по факту правки.
/// </summary>
public partial class InputsViewModel : ObservableObject
{
    private DiscreteInputsService? _service;

    // Последний снимок конфигурации, прочитанный из ПЛК (для сравнения).
    private int[]? _plcSelectors;
    private bool[]? _plcNoNc;

    // Входы с незавершённой записью — опрос их не трогает, чтобы не откатить правку.
    private readonly HashSet<int> _pendingWrites = new();
    private bool _applyingConfig;

    public ObservableCollection<DiscreteInputViewModel> Inputs { get; } = new();

    [ObservableProperty] private string _status = "";

    public InputsViewModel()
    {
        for (int i = 0; i < AppSettings.InputCount; i++)
        {
            var input = new DiscreteInputViewModel { Index = i, Name = FixedName(i) };
            input.PropertyChanged += OnInputPropertyChanged;
            Inputs.Add(input);
        }
    }

    /// <summary>Фиксированные наименования: 0 — контроль доступа, 1 — питание, 2 — авторежим, далее фидеры.</summary>
    private static string FixedName(int index) => index switch
    {
        0 => "Контроль доступа",
        1 => "Наличие питания",
        2 => "Авто режим",
        _ => $"Фидер {index - 2}"
    };

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

    /// <summary>
    /// Применение конфигурации из циклического опроса (UI-поток).
    /// Обновляет поля только если конфигурация в ПЛК изменилась;
    /// входы с незавершённой записью пропускаются.
    /// </summary>
    internal void ApplyConfig(InputConfig config)
    {
        if (_plcSelectors is not null
            && _plcSelectors.SequenceEqual(config.Selectors)
            && _plcNoNc!.SequenceEqual(config.NoNc))
            return;

        _plcSelectors = (int[])config.Selectors.Clone();
        _plcNoNc = (bool[])config.NoNc.Clone();

        _applyingConfig = true;
        try
        {
            int n = Math.Min(Inputs.Count, config.Selectors.Length);
            for (int i = 0; i < n; i++)
            {
                if (_pendingWrites.Contains(i))
                    continue;
                Inputs[i].Selector = config.Selectors[i];
                Inputs[i].IsNc = config.NoNc[i];
            }
        }
        finally
        {
            _applyingConfig = false;
        }
    }

    /// <summary>Сброс дискретных входов (импульс бита в слове управления ПЛК).</summary>
    [RelayCommand]
    private async Task ResetAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        try
        {
            await _service.ResetAsync();
            Status = "Дискретные входы сброшены";
        }
        catch (Exception ex) { Status = "Ошибка сброса: " + ex.Message; }
    }

    // ------------------------------------------------------------------
    // Запись в ПЛК сразу по факту изменения поля пользователем

    private void OnInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Изменения из опроса (ApplyConfig) обратно в ПЛК не пишем.
        if (_applyingConfig || sender is not DiscreteInputViewModel input)
            return;

        if (e.PropertyName == nameof(DiscreteInputViewModel.Selector))
            WriteField(input, s => s.WriteSelectorAsync(input.Index, input.Selector));
        else if (e.PropertyName == nameof(DiscreteInputViewModel.IsNc))
            WriteField(input, s => s.WriteNoNcAsync(input.Index, input.IsNc));
    }

    private async void WriteField(
        DiscreteInputViewModel input, Func<DiscreteInputsService, Task> write)
    {
        if (_service is null) { Status = "Нет подключения"; return; }

        _pendingWrites.Add(input.Index);
        // Оптимистично обновляем снимок — ближайший опрос не откатит правку.
        if (_plcSelectors is not null) _plcSelectors[input.Index] = input.Selector;
        if (_plcNoNc is not null) _plcNoNc[input.Index] = input.IsNc;

        try
        {
            await write(_service);
            Status = $"{input.Name}: записано";
        }
        catch (Exception ex)
        {
            Status = $"{input.Name}: ошибка записи — {ex.Message}";
        }
        finally
        {
            _pendingWrites.Remove(input.Index);
        }
    }
}
