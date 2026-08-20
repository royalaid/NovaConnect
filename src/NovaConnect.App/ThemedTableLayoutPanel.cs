using System.Drawing;

namespace NovaConnect.App;

internal sealed class ThemedTableLayoutPanel : TableLayoutPanel
{
    internal Color BorderColor { get; set; } = UiTheme.Border;

    internal ThemedTableLayoutPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
