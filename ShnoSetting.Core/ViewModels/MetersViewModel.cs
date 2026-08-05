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

    // Данные (из опроса)
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private float _u1, _u2, _u3;
    [ObservableProperty] private float _i1, _i2, _i3;
    [ObservableProperty] private float _p1, _p2, _p3, _pTotal;
    [ObservableProperty] private float _energy;
    [ObservableProperty] private int _serial;
    [ObservableProperty] private bool _commOk;
}

/// <summary>Счётчики: 6 слотов, настройка типа/адреса + данные из опроса.</summary>
public partial class MetersViewModel : ObservableObject
{
    private MetersService? _service;

    public ObservableCollection<MeterSlotViewModel> Slots { get; } = new();

    [ObservableProperty] private string _status = "";

    public MetersViewModel(int slotCount = 6)
    {
        for (int i = 1; i <= slotCount; i++)
            Slots.Add(new MeterSlotViewModel { Number = i });
    }

    internal void Attach(MetersService service) => _service = service;

    /// <summary>Обновление данных из циклического опроса (UI-поток).</summary>
    internal void ApplyData(MeterData?[] data)
    {
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
