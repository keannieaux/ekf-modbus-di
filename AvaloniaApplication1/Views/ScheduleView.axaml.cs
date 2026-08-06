using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ShnoSetting.Core.Schedule;
using ShnoSetting.Core.ViewModels;

namespace AvaloniaApplication1.Views;

public partial class ScheduleView : UserControl
{
    private static readonly FilePickerFileType CsvType =
        new("CSV") { Patterns = new[] { "*.csv" } };

    public ScheduleView()
    {
        InitializeComponent();
    }

    private ScheduleViewModel? ViewModel => DataContext as ScheduleViewModel;

    /// <summary>«Прочитать из RTU» — чтение графика из ПЛК и предпросмотр (без записи).</summary>
    private async void OnReadFromRtu(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
            return;

        var days = await ViewModel.ReadFromPlcAsync();
        if (days is null)
            return;

        await ShowPreview(
            "График, прочитанный из ПЛК",
            days,
            write: null,
            export: path => ViewModel.ExportCsv(path, days));
    }

    /// <summary>«Загрузить CSV» — выбор файла и предпросмотр с кнопкой «Записать».</summary>
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

        string? path = files.Count == 0 ? null : files[0].TryGetLocalPath();
        if (path is not null)
            await OpenCsvPreview(path);
    }

    // ------------------------------------------------------------------
    // Drag'n'Drop CSV

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        bool hasFiles = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.Background = hasFiles ? Brush.Parse("#ECEDEF") : Brushes.Transparent;
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
        => DropZone.Background = Brushes.Transparent;

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        DropZone.Background = Brushes.Transparent;

        var files = e.DataTransfer.TryGetFiles();
        string? path = files?.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
            return;

        if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            FileHint.Text = "Нужен CSV-файл с графиком";
            return;
        }

        await OpenCsvPreview(path);
    }

    // ------------------------------------------------------------------

    /// <summary>Разбор CSV и предпросмотр с кнопкой «Записать» (запись в ПЛК).</summary>
    private async Task OpenCsvPreview(string path)
    {
        if (ViewModel is null)
            return;

        var days = ViewModel.LoadCsv(path);
        if (days is null)
            return;

        FileHint.Text = "Файл: " + path;
        await ShowPreview(
            "Предпросмотр графика из CSV — проверьте и нажмите «Записать»",
            days,
            () => ViewModel.WriteToPlcAsync(days));
    }

    private async Task ShowPreview(
        string title, IReadOnlyList<ScheduleDay> days,
        Func<Task<bool>>? write, Func<string, bool>? export = null)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var window = new SchedulePreviewWindow(title, days, write, export);
        await window.ShowDialog(owner);
    }
}
