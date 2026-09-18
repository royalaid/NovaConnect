using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class StatusPresentationTests
{
    [Theory]
    [InlineData("Headset connected - 85%", "Connected")]
    [InlineData("Headset disconnected", "Disconnected")]
    [InlineData("Headset status unavailable", "Warning")]
    [InlineData("Review settings before switching", "Warning")]
    [InlineData("Starting...", "Neutral")]
    public void FromText_MapsRuntimeStatusToSemanticTone(string status, string expected)
    {
        StatusPresentation presentation = StatusPresentation.FromText(status);

        Assert.Equal(expected, presentation.Tone.ToString());
        Assert.Equal(status, presentation.Text);
    }
}
