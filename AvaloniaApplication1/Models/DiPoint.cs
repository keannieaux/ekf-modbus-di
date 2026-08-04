namespace AvaloniaApplication1.Models;

public sealed class DiPoint
{
    public required string Name { get; init; }
    public required int Index { get; init; }
    
    public int BitCoil => PLc.M(0+Index);
    public int SelectReg => PLc.V(10000+Index);
    public int NoNcCoil => PLc.M(10000+Index);
}