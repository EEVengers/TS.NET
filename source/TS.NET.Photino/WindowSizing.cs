using Photino.NET;
using System.Drawing;

namespace TS.NET.Photino;

public class WindowSizing
{
    private const uint BaseDpi = 96;
    private uint OldDpi = BaseDpi;

    private readonly int baseMinWidth;
    private readonly int baseMinHeight;
    private int baseWidth;
    private int baseHeight;

    public WindowSizing(int baseMinWidth, int baseMinHeight, int baseWidth, int baseHeight)
    {
        this.baseMinWidth = baseMinWidth;
        this.baseMinHeight = baseMinHeight;
        this.baseWidth = baseWidth;
        this.baseHeight = baseHeight;
    }

    public void UpdateSize(PhotinoWindow window)
    {
        uint newDpi = window.ScreenDpi;
        var absoluteDpi = (double)newDpi / BaseDpi;

        if (newDpi == OldDpi)
        {
            // User resized window so save values
            baseWidth = (int)(window.Size.Width / absoluteDpi);
            baseHeight = (int)(window.Size.Height / absoluteDpi);
            return;
        }
        OldDpi = newDpi;

        int width = (int)(baseWidth * absoluteDpi);
        int height = (int)(baseHeight * absoluteDpi);
        int minWidth = (int)(baseMinWidth * absoluteDpi);
        int minHeight = (int)(baseMinHeight * absoluteDpi);

        window.MinSize = new Point(minWidth, minHeight);
        window.Size = new Size(width, height);
    }
}
