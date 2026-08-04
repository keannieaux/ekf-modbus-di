using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace AvaloniaApplication1.ViewModels;

public partial class DiPointViewModel : ObservableValidator
{
    public DiPointViewModel(DiPoint point) => Point = point;

    public DiPoint Point { get; }
    public string Name => Point.Name;

    [ObservableProperty] public partial bool Bit { get; set; }
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0,256, ErrorMessage = "Допустимо от  0 до 256")]
    public partial int Select { get; set; }
    [ObservableProperty] public partial bool IsNc { get; set; }
}
