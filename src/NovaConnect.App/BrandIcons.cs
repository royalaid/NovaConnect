using System.Drawing;
using System.Reflection;

namespace NovaConnect.App;

internal static class BrandIcons
{
    private const string ResourceName = "NovaConnect.App.Assets.NovaConnect.ico";

    internal static Icon CreateTrayIcon() => CreateIcon(new Size(16, 16));

    internal static Icon CreateTrayIcon(StatusTone tone) => CreateIcon(
        new Size(16, 16),
        tone switch
        {
            StatusTone.Connected => "NovaConnect.App.Assets.Headphones.ico",
            StatusTone.Disconnected => "NovaConnect.App.Assets.Speaker.ico",
            _ => ResourceName
        });

    internal static Icon CreateWindowIcon() => CreateIcon(new Size(32, 32));

    internal static Bitmap CreateHeaderBitmap()
    {
        using Icon icon = CreateIcon(new Size(48, 48));
        return icon.ToBitmap();
    }

    private static Icon CreateIcon(Size size, string resourceName = ResourceName)
    {
        Assembly assembly = typeof(BrandIcons).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded icon resource '{resourceName}' was not found.");
        using var source = new Icon(stream, size);
        return (Icon)source.Clone();
    }
}
