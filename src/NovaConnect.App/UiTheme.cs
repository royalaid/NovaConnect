using System.Drawing;

namespace NovaConnect.App;

internal static class UiTheme
{
    internal static readonly Color Window = Color.FromArgb(17, 19, 24);
    internal static readonly Color Surface = Color.FromArgb(25, 29, 36);
    internal static readonly Color SurfaceElevated = Color.FromArgb(32, 37, 46);
    internal static readonly Color Border = Color.FromArgb(51, 58, 70);
    internal static readonly Color ControlBorder = Color.FromArgb(100, 112, 132);
    internal static readonly Color Text = Color.FromArgb(244, 247, 250);
    internal static readonly Color MutedText = Color.FromArgb(170, 180, 195);
    internal static readonly Color Accent = Color.FromArgb(9, 97, 205);
    internal static readonly Color AccentHover = Color.FromArgb(20, 112, 222);
    internal static readonly Color Focus = Color.FromArgb(79, 155, 255);
    internal static readonly Color Danger = Color.FromArgb(255, 123, 132);

    internal static readonly Font BodyFont = new("Segoe UI", 9.5F);
    internal static readonly Font SmallFont = new("Segoe UI", 9F);
    internal static readonly Font SmallSemiboldFont = new("Segoe UI Semibold", 9F);
    internal static readonly Font ButtonFont = new("Segoe UI Semibold", 9.5F);
    internal static readonly Font CaptionFont = new("Segoe UI Semibold", 8.5F);
    internal static readonly Font SectionFont = new("Segoe UI Semibold", 11F, FontStyle.Bold);
    internal static readonly Font TitleFont = new("Segoe UI Semibold", 17F, FontStyle.Bold);

    internal static Color StatusForeground(StatusTone tone) => tone switch
    {
        StatusTone.Connected => Color.FromArgb(112, 214, 155),
        StatusTone.Disconnected => Color.FromArgb(186, 194, 205),
        StatusTone.Warning => Color.FromArgb(255, 184, 108),
        _ => Color.FromArgb(128, 181, 242)
    };

    internal static Color StatusBackground(StatusTone tone) => tone switch
    {
        StatusTone.Connected => Color.FromArgb(24, 55, 43),
        StatusTone.Disconnected => Color.FromArgb(39, 44, 53),
        StatusTone.Warning => Color.FromArgb(61, 43, 25),
        _ => Color.FromArgb(25, 45, 69)
    };
}
