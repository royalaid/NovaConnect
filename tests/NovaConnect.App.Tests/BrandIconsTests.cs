using System.Drawing;
using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class BrandIconsTests
{
    [Fact]
    public void CreateTrayIcon_LoadsDedicatedSixteenPixelResource()
    {
        using Icon icon = BrandIcons.CreateTrayIcon();

        Assert.Equal(new Size(16, 16), icon.Size);
        using Bitmap bitmap = icon.ToBitmap();
        Assert.True(bitmap.GetPixel(8, 8).A > 0);
    }
}
