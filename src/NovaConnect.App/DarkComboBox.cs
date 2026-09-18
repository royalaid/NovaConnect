using System.Drawing;

namespace NovaConnect.App;

internal sealed class DarkComboBox : ComboBox
{
    private const int WmPaint = 0x000F;
    private const int WmNcPaint = 0x0085;
    private const int WmPrintClient = 0x0318;

    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg is WmPaint or WmNcPaint or WmPrintClient)
        {
            DrawArrowButton();
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Invalidate();
    }

    protected override void OnLeave(EventArgs e)
    {
        base.OnLeave(e);
        Invalidate();
    }

    private void DrawArrowButton()
    {
        if (!IsHandleCreated || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        int arrowWidth = Math.Max(18, (int)Math.Round(22 * DeviceDpi / 96F));
        var bounds = new Rectangle(
            ClientSize.Width - arrowWidth,
            1,
            arrowWidth - 1,
            Math.Max(1, ClientSize.Height - 2));
        using Graphics graphics = CreateGraphics();
        using var background = new SolidBrush(Enabled ? UiTheme.SurfaceElevated : UiTheme.Surface);
        graphics.FillRectangle(background, bounds);
        using var divider = new Pen(UiTheme.ControlBorder);
        graphics.DrawLine(divider, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom);

        int centerX = bounds.Left + (bounds.Width / 2);
        int centerY = bounds.Top + (bounds.Height / 2) + 1;
        int halfWidth = Math.Max(3, (int)Math.Round(4 * DeviceDpi / 96F));
        int height = Math.Max(2, (int)Math.Round(3 * DeviceDpi / 96F));
        Point[] arrow =
        [
            new(centerX - halfWidth, centerY - height),
            new(centerX + halfWidth, centerY - height),
            new(centerX, centerY + height)
        ];
        using var foreground = new SolidBrush(Enabled ? UiTheme.Text : UiTheme.MutedText);
        graphics.FillPolygon(foreground, arrow);
        using var boundary = new Pen(Focused ? UiTheme.Focus : UiTheme.ControlBorder);
        graphics.DrawRectangle(boundary, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }
}
