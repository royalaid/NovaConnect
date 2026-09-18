using System.Drawing;

namespace NovaConnect.App;

internal sealed class DarkToolStripColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => UiTheme.Surface;
    public override Color ImageMarginGradientBegin => UiTheme.Surface;
    public override Color ImageMarginGradientMiddle => UiTheme.Surface;
    public override Color ImageMarginGradientEnd => UiTheme.Surface;
    public override Color MenuBorder => UiTheme.Border;
    public override Color MenuItemBorder => UiTheme.Focus;
    public override Color MenuItemSelected => UiTheme.SurfaceElevated;
    public override Color MenuItemSelectedGradientBegin => UiTheme.SurfaceElevated;
    public override Color MenuItemSelectedGradientEnd => UiTheme.SurfaceElevated;
    public override Color MenuItemPressedGradientBegin => UiTheme.SurfaceElevated;
    public override Color MenuItemPressedGradientMiddle => UiTheme.SurfaceElevated;
    public override Color MenuItemPressedGradientEnd => UiTheme.SurfaceElevated;
    public override Color SeparatorDark => UiTheme.Border;
    public override Color SeparatorLight => UiTheme.Border;
    public override Color CheckBackground => UiTheme.Accent;
    public override Color CheckSelectedBackground => UiTheme.AccentHover;
    public override Color CheckPressedBackground => UiTheme.AccentHover;
}
