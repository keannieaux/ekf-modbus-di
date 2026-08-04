namespace AvaloniaApplication1.Models;

public static class PLc
{
    public const int VOffset = 512;
    public const int MOffset = 3072;
    
    public static int V(int n) => n + VOffset;
    public static int M(int n) => n + MOffset;
}