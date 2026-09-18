using System.Runtime.InteropServices;

namespace NovaConnect.App;

internal static class WindowTheme
{
    private const int UseImmersiveDarkModeBefore20H1 = 19;
    private const int UseImmersiveDarkMode = 20;

    internal static void TryEnableDarkTitleBar(IntPtr windowHandle)
    {
        int enabled = 1;
        if (DwmSetWindowAttribute(
                windowHandle,
                UseImmersiveDarkMode,
                ref enabled,
                sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(
                windowHandle,
                UseImmersiveDarkModeBefore20H1,
                ref enabled,
                sizeof(int));
        }
    }

    internal static void TryEnableDarkControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            string theme = control is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer";
            _ = SetWindowTheme(control.Handle, theme, null);
            TryEnableDarkControls(control);
            control.Invalidate();
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr windowHandle,
        string? subApplicationName,
        string? subIdentifierList);
}
