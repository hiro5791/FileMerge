using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace FileMerge.Services;

/// <summary>
/// Paints the title bar to match the app theme. Without this, a dark window keeps a white
/// caption bar, which reads as a rendering bug rather than a style choice.
/// </summary>
public static class WindowChrome
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    /// <summary>Windows 10 builds before 19041 used attribute 19 for the same flag.</summary>
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyTitleBarTheme(Window window, bool dark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        int value = dark ? 1 : 0;

        try
        {
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref value, sizeof(int));
            }
        }
        catch (DllNotFoundException)
        {
            // Nothing to do on a system without dwmapi; the window simply keeps the light caption.
        }
    }
}
