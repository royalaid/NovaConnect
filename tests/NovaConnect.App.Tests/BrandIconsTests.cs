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

    [Theory]
    [InlineData("Headset connected, 85%", "Headphones")]
    [InlineData("Headset connected, 85% (switching disabled)", "Headphones")]
    [InlineData("Headset disconnected", "Speaker")]
    [InlineData("Headset disconnected (switching disabled)", "Speaker")]
    [InlineData("Starting…", "NovaConnect")]
    [InlineData("Headset status unavailable", "NovaConnect")]
    [InlineData("Configuration required", "NovaConnect")]
    [InlineData("Review settings before switching", "NovaConnect")]
    public void CreateTrayIcon_UsesConnectionGlyphForRuntimeStatus(string status, string asset)
    {
        using Icon icon = BrandIcons.CreateTrayIcon(StatusPresentation.FromText(status).Tone);
        using Stream stream = typeof(BrandIcons).Assembly.GetManifestResourceStream(
            $"NovaConnect.App.Assets.{asset}.ico")!;
        using var expected = new Icon(stream, new Size(16, 16));
        using Bitmap actualBitmap = icon.ToBitmap();
        using Bitmap expectedBitmap = expected.ToBitmap();

        Assert.Equal(new Size(16, 16), icon.Size);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(expectedBitmap.GetPixel(x, y), actualBitmap.GetPixel(x, y));
            }
        }
    }

    [Fact]
    public void ConnectionIcons_HaveDistinctGlyphs()
    {
        using Icon connected = BrandIcons.CreateTrayIcon(StatusTone.Connected);
        using Icon disconnected = BrandIcons.CreateTrayIcon(StatusTone.Disconnected);
        using Bitmap headphones = connected.ToBitmap();
        using Bitmap speaker = disconnected.ToBitmap();

        // Headphones leave the center open; the speaker's cone fills it.
        Assert.True(headphones.GetPixel(7, 8).R < 100);
        Assert.True(speaker.GetPixel(7, 8).R > 200);
    }
}
