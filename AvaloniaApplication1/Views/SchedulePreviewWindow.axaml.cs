using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShnoSetting.Core.Schedule;

namespace AvaloniaApplication1.Views;

/// <summary>
/// Всплывающее окно предпросмотра графика (366 дней).
/// Два режима: просмотр (чтение из ПЛК) и подтверждение записи (из CSV).
/// В режиме просмотра видна кнопка «Скачать CSV», в режиме записи — «Записать».
/// </summary>
public partial class SchedulePreviewWindow : Window
{
    private static readonly FilePickerFileType CsvType =
        new("CSV") { Patterns = new[] { "*.csv" } };

    private readonly Func<Task<bool>>? _write;
    private readonly Func<string, bool>? _export;

    public SchedulePreviewWindow()
    {
        InitializeComponent();
    }

    /// <param name="title">Подзаголовок: источник данных (ПЛК или файл).</param>
    /// <param name="days">Дни графика.</param>
    /// <param name="write">Колбэк записи в ПЛК; null — режим просмотра без кнопки «Записать».</param>
    /// <param name="export">Колбэк сохранения в CSV по пути; null — без кнопки «Скачать CSV».</param>
    public SchedulePreviewWindow(
        string title, IReadOnlyList<ScheduleDay> days,
        Func<Task<bool>>? write, Func<string, bool>? export = null)
        : this()
    {
        TitleText.Text = title;
        RowsList.ItemsSource = days.Select(ScheduleDayRow.FromDay).ToList();

        _write = write;
        WriteButton.IsVisible = write is not null;

        _export = export;
        ExportButton.IsVisible = export is not null;
    }

    private async void OnWrite(object? sender, RoutedEventArgs e)
    {
        if (_write is null)
            return;

        WriteButton.IsEnabled = false;
        StatusText.Text = "Запись в ПЛК…";

        bool ok = await _write();
        if (ok)
        {
            Close();
            return;
        }

        StatusText.Text = "Ошибка записи — подробности в статусе главного окна";
        WriteButton.IsEnabled = true;
    }

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (_export is null)
            return;

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить график в CSV",
            SuggestedFileName = $"schedule-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[] { CsvType }
        });

        string? path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        bool ok = _export(path);
        StatusText.Text = ok ? "CSV сохранён: " + path : "Ошибка сохранения CSV";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
