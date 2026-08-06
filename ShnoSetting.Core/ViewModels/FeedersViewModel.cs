using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ShnoSetting.Core.Settings;

namespace ShnoSetting.Core.ViewModels;

/// <summary>Индикатор одного фидера: номер + состояние (выход соответствующего входа).</summary>
public partial class FeederViewModel : ObservableObject
{
    private readonly DiscreteInputViewModel _input;
    private readonly FeedersViewModel _owner;

    /// <summary>Номер фидера (1..61).</summary>
    public int Number { get; }

    /// <summary>
    /// Состояние фидера — инверсия колонки «Выход» в таблице входов
    /// (1 в ПЛК = выключен, 0 = включён).
    /// </summary>
    public bool IsOn => !_input.OutputValue;

    /// <summary>Фидер используется (назначение входа ≠ маркеру «не используется»).</summary>
    public bool IsUsed => _input.Selector != _owner.UnusedSelectorValue;

    /// <summary>Непрозрачность слота: неиспользуемые фидеры скрыты, но место сохраняется.</summary>
    public double SlotOpacity => IsUsed ? 1.0 : 0.0;

    public FeederViewModel(DiscreteInputViewModel input, FeedersViewModel owner)
    {
        _input = input;
        _owner = owner;
        Number = input.Index - FeedersViewModel.FirstFeederInput + 1; // вход 3 → фидер 1
        _input.PropertyChanged += OnInputChanged;
    }

    private void OnInputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscreteInputViewModel.OutputValue))
            OnPropertyChanged(nameof(IsOn));
        else if (e.PropertyName == nameof(DiscreteInputViewModel.Selector))
        {
            OnPropertyChanged(nameof(IsUsed));
            OnPropertyChanged(nameof(SlotOpacity));
        }
    }
}

/// <summary>
/// Состояния фидеров: отображение выходов фидерных входов (3..63) из опроса.
/// Только просмотр. Отображаемые фидеры определяются автоматически по назначению
/// входов: фидер показан, если назначение его входа ≠ UnusedSelectorValue.
/// Слоты сохраняются: между используемыми фидерами остаются пустые места.
/// </summary>
public partial class FeedersViewModel : ObservableObject
{
    /// <summary>Входы 0..2 — служебные (доступ, питание, авторежим), фидеры — со входа 3.</summary>
    public const int FirstFeederInput = 3;

    public const int MaxFeeders = AppSettings.InputCount - FirstFeederInput;

    private readonly InputsViewModel _inputs;

    public ObservableCollection<FeederViewModel> VisibleFeeders { get; } = new();

    /// <summary>Значение назначения входа — маркер «вход не используется» (из профиля).</summary>
    [ObservableProperty] private int _unusedSelectorValue = 100;

    public FeedersViewModel(InputsViewModel inputs)
    {
        _inputs = inputs;
        // Перестраиваем список при изменении назначения любого фидерного входа.
        for (int i = FirstFeederInput; i < _inputs.Inputs.Count; i++)
            _inputs.Inputs[i].PropertyChanged += OnFeederInputChanged;
        Rebuild();
    }

    partial void OnUnusedSelectorValueChanged(int value) => Rebuild();

    private void OnFeederInputChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DiscreteInputViewModel.Selector))
            Rebuild();
    }

    /// <summary>
    /// Слоты 1..последний используемый фидер. Неиспользуемые остаются в списке
    /// невидимыми заглушками, чтобы используемые фидеры держали свои позиции.
    /// </summary>
    private void Rebuild()
    {
        int lastUsed = -1;
        int count = Math.Min(MaxFeeders, _inputs.Inputs.Count - FirstFeederInput);
        for (int i = 0; i < count; i++)
        {
            if (_inputs.Inputs[FirstFeederInput + i].Selector != UnusedSelectorValue)
                lastUsed = i;
        }

        VisibleFeeders.Clear();
        for (int i = 0; i <= lastUsed; i++)
            VisibleFeeders.Add(new FeederViewModel(_inputs.Inputs[FirstFeederInput + i], this));
    }
}
