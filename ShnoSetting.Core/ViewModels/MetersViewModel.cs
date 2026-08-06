using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Services;

namespace ShnoSetting.Core.ViewModels;

public partial class MeterSlotViewModel : ObservableObject
{
    /// <summary>Номер слота 1..6.</summary>
    public int Number { get; init; }

    // Настройки (запись в ПЛК)
    [ObservableProperty] private int _type;      // 0 = CE318(CE208), 1 = CC301(CC101)
    [ObservableProperty] private int _address;   // 0 = слот отключён

    /// <summary>Слот добавлен пользователем вручную (ещё не подтверждён опросом).</summary>
    public bool IsUserAdded { get; set; }

    // Данные (из опроса)
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private float _u1, _u2, _u3;
    [ObservableProperty] private float _i1, _i2, _i3;
    [ObservableProperty] private float _p1, _p2, _p3, _pTotal;
    [ObservableProperty] private float _energy;
    [ObservableProperty] private int _serial;
    [ObservableProperty] private bool _commOk;
}

/// <summary>Счётчики: 6 слотов, настройки (тип/адрес) и данные из опроса, запись по кнопке.</summary>
public partial class MetersViewModel : ObservableObject
{
    private MetersService? _service;

    // Последний снимок настроек, прочитанный из ПЛК (для сравнения).
    private MeterSlotConfig[]? _plcConfig;

    /// <summary>Все слоты профиля (1..6).</summary>
    public ObservableCollection<MeterSlotViewModel> Slots { get; } = new();

    /// <summary>Отображаемые слоты: активные по опросу + добавленные вручную.</summary>
    public ObservableCollection<MeterSlotViewModel> VisibleSlots { get; } = new();

    [ObservableProperty] private string _status = "";

    public MetersViewModel(int slotCount = 6)
    {
        for (int i = 1; i <= slotCount; i++)
            Slots.Add(new MeterSlotViewModel { Number = i });
    }

    internal void Attach(MetersService service)
    {
        _service = service;
        // Новое подключение: сбрасываем ручные добавления,
        // видимые слоты определятся первым циклом опроса.
        _plcConfig = null;
        foreach (var slot in Slots)
            slot.IsUserAdded = false;
        VisibleSlots.Clear();
    }

    /// <summary>Добавить следующий скрытый слот для ручной настройки.</summary>
    [RelayCommand]
    private void AddSlot()
    {
        var slot = Slots.FirstOrDefault(s => !VisibleSlots.Contains(s));
        if (slot is null) { Status = "Все слоты уже добавлены"; return; }

        slot.IsUserAdded = true;
        SetVisible(slot, true);
        Status = $"Слот {slot.Number}: введите тип и адрес счётчика, затем нажмите «Записать»";
    }

    /// <summary>Показать/скрыть слот, сохраняя порядок по номеру.</summary>
    private void SetVisible(MeterSlotViewModel slot, bool visible)
    {
        bool shown = VisibleSlots.Contains(slot);
        if (visible && !shown)
        {
            int pos = VisibleSlots.Count(s => s.Number < slot.Number);
            VisibleSlots.Insert(pos, slot);
        }
        else if (!visible && shown)
        {
            VisibleSlots.Remove(slot);
        }
    }

    /// <summary>Обновление настроек и данных из циклического опроса (UI-поток).</summary>
    internal void ApplyData(MetersSnapshot snapshot)
    {
        ApplyConfig(snapshot.Config);

        var data = snapshot.Data;
        int n = Math.Min(Slots.Count, data.Length);
        for (int i = 0; i < n; i++)
        {
            var slot = Slots[i];
            var d = data[i];
            slot.IsActive = d is not null;
            if (d is null) continue;

            slot.U1 = d.Voltages[0]; slot.U2 = d.Voltages[1]; slot.U3 = d.Voltages[2];
            slot.I1 = d.Currents[0]; slot.I2 = d.Currents[1]; slot.I3 = d.Currents[2];
            slot.P1 = d.Powers[0]; slot.P2 = d.Powers[1]; slot.P3 = d.Powers[2];
            slot.PTotal = d.Powers[3];
            slot.Energy = d.Energy;
            slot.Serial = d.Serial;
            slot.CommOk = d.CommOk;
        }
    }

    /// <summary>
    /// Применение настроек слотов из опроса.
    /// Обновляет поля только если настройки в ПЛК изменились —
    /// несохранённые правки пользователя не затираются.
    /// </summary>
    private void ApplyConfig(MeterSlotConfig[] config)
    {
        bool changed = _plcConfig is null || _plcConfig.Length != config.Length;
        if (!changed)
        {
            for (int i = 0; i < config.Length; i++)
            {
                if (_plcConfig![i].Type != config[i].Type || _plcConfig[i].Address != config[i].Address)
                {
                    changed = true;
                    break;
                }
            }
        }
        if (!changed) return;

        _plcConfig = config
            .Select(c => new MeterSlotConfig { Type = c.Type, Address = c.Address })
            .ToArray();

        int n = Math.Min(Slots.Count, config.Length);
        for (int i = 0; i < n; i++)
        {
            Slots[i].Type = (int)config[i].Type;
            Slots[i].Address = config[i].Address;
            // Автоопределение: слот виден, если он активен в ПЛК или добавлен вручную.
            SetVisible(Slots[i], config[i].IsActive || Slots[i].IsUserAdded);
        }
    }

    [RelayCommand]
    private async Task ReadConfigAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        try
        {
            var config = await _service.ReadConfigAsync();
            int n = Math.Min(Slots.Count, config.Length);
            for (int i = 0; i < n; i++)
            {
                Slots[i].Type = (int)config[i].Type;
                Slots[i].Address = config[i].Address;
            }
            Status = $"Прочитаны настройки {n} слотов";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
    }

    [RelayCommand]
    private async Task WriteSlotAsync(MeterSlotViewModel? slot)
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        if (slot is null) return;
        try
        {
            await _service.WriteConfigAsync(slot.Number - 1,
                new MeterSlotConfig { Type = (MeterType)slot.Type, Address = slot.Address });
            Status = slot.Address == 0
                ? $"Слот {slot.Number} отключён"
                : $"Слот {slot.Number}: тип {slot.Type}, адрес {slot.Address}";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
    }
}
