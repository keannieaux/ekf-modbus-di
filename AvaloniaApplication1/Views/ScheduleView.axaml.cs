using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class ScheduleView : UserControl
{
    private static readonly FilePickerFileType CsvType =
        new("CSV") { Patterns = new[] { "*.csv" } };

    private string? _csvPath;

    public ScheduleView()
    {
        InitializeComponent();
    }

    private ScheduleViewModel? ViewModel => DataContext as ScheduleViewModel;

    /// <summary>«Загрузить CSV» — выбор файла графика; сама запись идёт по «Записать в RTU».</summary>
    private async void OnLoadCsv(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите CSV с графиком",
            AllowMultiple = false,
            FileTypeFilter = new[] { CsvType }
        });

        if (files.Count == 0)
            return;

        _csvPath = files[0].TryGetLocalPath();
        FileHint.Text = _csvPath is null ? "Файл не выбран" : "Файл: " + _csvPath;
    }

    /// <summary>«Записать в RTU» — отправка выбранного CSV в ПЛК.</summary>
    private void OnWriteToRtu(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (string.IsNullOrWhiteSpace(_csvPath))
        {
            FileHint.Text = "Сначала выберите файл кнопкой «Загрузить CSV»";
            return;
        }

        if (ViewModel.ImportCsvCommand.CanExecute(_csvPath))
            ViewModel.ImportCsvCommand.Execute(_csvPath);
    }

    /// <summary>«Прочитать из RTU» — выгрузка графика из ПЛК в файл.</summary>
    private async void OnReadFromRtu(object? sender, RoutedEventArgs e) => await ExportAsync();

    /// <summary>
    /// «Скачать CSV» — в текущем API ViewModel делает то же самое, что «Прочитать из RTU»:
    /// таблицы в памяти нет, поэтому единственный источник данных — сам ПЛК.
    /// </summary>
    private async void OnDownloadCsv(object? sender, RoutedEventArgs e) => await ExportAsync();

    private async Task ExportAsync()
    {
        if (ViewModel is null)
            return;

        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить график в CSV",
            SuggestedFileName = $"schedule-{DateTime.Now:yyyyMMdd-HHmm}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = new[] { CsvType }
        });

        string? path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (ViewModel.ExportCsvCommand.CanExecute(path))
            ViewModel.ExportCsvCommand.Execute(path);
    }
}
