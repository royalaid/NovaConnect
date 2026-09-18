using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Windows.Forms;
using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class SettingsFormLayoutTests
{
    [Fact]
    public void Constructor_UsesCompactDarkNonScrollingLayout()
    {
        RunInSta(() =>
        {
            AudioEndpoint[] endpoints =
            [
                new("headset-out", "Headphones (SteelSeries Arctis Nova 5)", AudioFlow.Playback, true),
                new("speakers", "Speakers", AudioFlow.Playback, true),
                new("headset-mic", "Microphone (SteelSeries Arctis Nova 5)", AudioFlow.Capture, true),
                new("desk-mic", "Desk microphone", AudioFlow.Capture, true)
            ];
            var configuration = new AppConfiguration
            {
                HeadsetPlaybackId = "headset-out",
                FallbackPlaybackId = "speakers",
                HeadsetCaptureId = "headset-mic",
                FallbackCaptureId = "desk-mic"
            };

            using var form = new SettingsForm(
                endpoints,
                configuration,
                "Headset connected, 85%",
                startWithWindows: true);

            Assert.Equal(new Size(820, 470), form.ClientSize);
            Assert.Equal(UiTheme.Window, form.BackColor);
            Assert.DoesNotContain(
                Descendants(form).OfType<ScrollableControl>(),
                control => control.AutoScroll);
            Assert.DoesNotContain(Descendants(form), control => control is GroupBox);
            ComboBox[] endpointSelectors = Descendants(form).OfType<ComboBox>().ToArray();
            Assert.Equal(4, endpointSelectors.Length);
            Assert.All(endpointSelectors, selector => Assert.False(string.IsNullOrWhiteSpace(selector.AccessibleName)));
            NumericUpDown interval = Assert.Single(Descendants(form).OfType<NumericUpDown>());
            Assert.Equal("Detection interval in milliseconds", interval.AccessibleName);
            Label status = Assert.Single(
                Descendants(form).OfType<Label>(),
                label => label.AccessibleName == "Headset status");
            Assert.Contains("Headset connected, 85%", status.Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void UpdateStatus_Disconnect_RefreshesBannerTextAndTone()
    {
        RunInSta(() =>
        {
            AudioEndpoint[] endpoints =
            [
                new("headset-out", "Headphones (SteelSeries Arctis Nova 5)", AudioFlow.Playback, true),
                new("speakers", "Speakers", AudioFlow.Playback, true),
                new("headset-mic", "Microphone (SteelSeries Arctis Nova 5)", AudioFlow.Capture, true),
                new("desk-mic", "Desk microphone", AudioFlow.Capture, true)
            ];
            var configuration = new AppConfiguration
            {
                HeadsetPlaybackId = "headset-out",
                FallbackPlaybackId = "speakers",
                HeadsetCaptureId = "headset-mic",
                FallbackCaptureId = "desk-mic"
            };

            using var form = new SettingsForm(
                endpoints,
                configuration,
                "Headset connected, 85%",
                startWithWindows: false);

            form.UpdateStatus("Headset disconnected");

            Label status = Assert.Single(
                Descendants(form).OfType<Label>(),
                label => label.AccessibleName == "Headset status");
            Assert.Contains("Headset disconnected", status.Text, StringComparison.Ordinal);
            Assert.Equal(UiTheme.StatusForeground(StatusTone.Disconnected), status.ForeColor);
            Assert.Equal(UiTheme.StatusBackground(StatusTone.Disconnected), status.BackColor);
        });
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                error = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
