using AvaloniaApplication1.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaApplication1.ViewModels;

public partial class DiPointViewModel : ObservableObject
{
    public DiPointViewModel(DiPoint point) => Point = point;

    public DiPoint Point { get; }
    public string Name => Point.Name;

    [ObservableProperty] public partial bool Bit { get; set; }
    [ObservableProperty] public partial int Select { get; set; }
    [ObservableProperty] public partial bool IsNc { get; set; }
}
