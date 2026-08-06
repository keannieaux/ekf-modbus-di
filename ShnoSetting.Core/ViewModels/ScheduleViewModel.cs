using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using ShnoSetting.Core.Schedule;

namespace ShnoSetting.Core.ViewModels;

/// <summary>
/// График работы: чтение из ПЛК и запись по явной команде.
/// Запись идёт через предпросмотр: CSV разбирается в память, показывается
/// пользователю во всплывающем окне и только потом пишется в ПЛК.
/// Диалоги выбора файла и окно предпросмотра — на стороне UI.
/// </summary>
public partial class ScheduleViewModel : ObservableObject
{
    private ScheduleService? _service;

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    internal void Attach(ScheduleService service) => _service = service;

    /// <summary>Чтение графика из ПЛК в память (для окна предпросмотра).</summary>
    public async Task<IReadOnlyList<ScheduleDay>?> ReadFromPlcAsync()
    {
        if (_service is null) { Status = "Нет подключения"; return null; }

        IsBusy = true;
        Status = "Чтение графика из ПЛК…";
        try
        {
            var days = await _service.ReadFromPlcAsync();
            Status = $"График прочитан из ПЛК: {days.Count} дней";
            return days;
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; return null; }
        finally { IsBusy = false; }
    }

    /// <summary>Разбор CSV-файла в память (для окна предпросмотра перед записью).</summary>
    public IReadOnlyList<ScheduleDay>? LoadCsv(string path)
    {
        try
        {
            var days = ScheduleCsv.Parse(File.ReadAllText(path));
            Status = $"CSV загружен: {days.Count} дней";
            return days;
        }
        catch (FormatException ex) { Status = "Ошибка CSV: " + ex.Message; return null; }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; return null; }
    }

    /// <summary>Запись предпросмотренного графика в ПЛК.</summary>
    public async Task<bool> WriteToPlcAsync(IReadOnlyList<ScheduleDay> days)
    {
        if (_service is null) { Status = "Нет подключения"; return false; }

        IsBusy = true;
        Status = "Запись графика в ПЛК…";
        try
        {
            await _service.WriteToPlcAsync(days);
            Status = "График записан в ПЛК";
            return true;
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; return false; }
        finally { IsBusy = false; }
    }

    /// <summary>Сохранение предпросмотренного графика из памяти в CSV-файл.</summary>
    public bool ExportCsv(string path, IReadOnlyList<ScheduleDay> days)
    {
        try
        {
            // UTF-8 с BOM — иначе Excel открывает кириллицу как ANSI и показывает кракозябры.
            File.WriteAllText(path, ScheduleCsv.Write(days), new UTF8Encoding(true));
            Status = "График выгружен в CSV";
            return true;
        }
        catch (Exception ex) { Status = "Ошибка: " + ex.Message; return false; }
    }
}
