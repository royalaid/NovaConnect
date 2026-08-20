using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class StartupMonitorPolicyTests
{
    [Theory]
    [InlineData(true, 0, false)]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, false)]
    public void ShouldStart_RequiresSavedValidConfiguration(
        bool requiresReview,
        int validationErrorCount,
        bool expected)
    {
        bool result = StartupMonitorPolicy.ShouldStart(requiresReview, validationErrorCount);

        Assert.Equal(expected, result);
    }
}
