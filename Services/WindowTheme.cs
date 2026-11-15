using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ColecaoMidias.Services;

/// <summary>Pinta a barra de título do Windows (escura ou clara) de acordo com o tema do app.</summary>
public static class WindowTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Window window, bool dark)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
                return;

            var value = dark ? 1 : 0;
            // 20 = Windows 10 (2004+) e 11; 19 = versões anteriores do Windows 10.
            if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
        }
        catch (Exception)
        {
            // Enfeite apenas: se não funcionar, a barra de título fica no padrão do Windows.
        }
    }

    public static void ApplyToAll(bool dark)
    {
        foreach (Window window in Application.Current.Windows)
            Apply(window, dark);
    }
}
