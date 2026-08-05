using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Services;

namespace ShnoSetting.Core.ViewModels;

/// <summary>Время контроллера: отображение + синхронизация с ПК.</summary>
public partial class ClockViewModel : ObservableObject
{
    private ClockService? _service;

    [ObservableProperty] private string _plcTimeText = "—";
    [ObservableProperty] private string _status = "";

    internal void Attach(ClockService service) => _service = service;

    internal void ApplyTime(DateTime? time)
        => PlcTimeText = time?.ToString("dd.MM.yyyy HH:mm:ss") ?? "—";

    [RelayCommand]
    private async Task SyncToPcAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        try
        {
            await _service.SyncToPcAsync();
            Status = "Время синхронизировано с ПК";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
    }
}
