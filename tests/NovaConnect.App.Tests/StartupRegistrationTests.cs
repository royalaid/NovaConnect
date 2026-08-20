using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class StartupRegistrationTests
{
    [Theory]
    [InlineData("\"C:\\Apps\\NovaConnect.exe\" --background", "C:\\Apps\\NovaConnect.exe", true)]
    [InlineData("\"C:\\Old\\NovaConnect.exe\" --background", "C:\\Apps\\NovaConnect.exe", false)]
    [InlineData("something else", "C:\\Apps\\NovaConnect.exe", false)]
    [InlineData(null, "C:\\Apps\\NovaConnect.exe", false)]
    public void IsExpectedCommand_MatchesOnlyCurrentExecutable(
        string? command,
        string executablePath,
        bool expected)
    {
        Assert.Equal(expected, StartupRegistration.IsExpectedCommand(command, executablePath));
    }
}
