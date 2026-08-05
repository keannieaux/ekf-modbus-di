using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShnoSetting.Core.Schedule;

namespace ShnoSetting.Core.ViewModels;

/// <summary>
/// График работы: запись из CSV / чтение в CSV по явной команде.
/// Команды принимают путь к файлу (диалог выбора файла — на стороне UI).
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private ScheduleService? _service;

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    internal void Attach(ScheduleService service) => _service = service;

    [RelayCommand]
    private async Task ImportCsvAsync(string? path)
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        if (string.IsNullOrWhiteSpace(path)) { Status = "Не выбран файл"; return; }

        IsBusy = true;
        try
        {
            await _service.ImportFromCsvAsync(path);
            Status = "График записан в ПЛК";
        }
        catch (FormatException ex) { Status = "Ошибка CSV: " + ex.Message; }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExportCsvAsync(string? path)
    {
        if (_service is null) { Status = "Нет подключения"; return; }
        if (string.IsNullOrWhiteSpace(path)) { Status = "Не выбран файл"; return; }

        IsBusy = true;
        try
        {
            await _service.ReadToCsvAsync(path);
            Status = "График выгружен в CSV";
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
        finally { IsBusy = false; }
    }
}
