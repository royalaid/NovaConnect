using NovaConnect.App;
using Xunit;

namespace NovaConnect.App.Tests;

public sealed class FileLoggerTests
{
    [Fact]
    public void Info_WhenLogWouldExceedLimit_RotatesOnePreviousFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), "NovaConnect.Tests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "NovaConnect.log");
        try
        {
            var logger = new FileLogger(path, maxBytes: 256);

            for (int index = 0; index < 20; index++)
            {
                logger.Info($"message-{index:D2}-with-enough-padding-to-force-rotation");
            }

            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path + ".1"));
            Assert.InRange(new FileInfo(path).Length, 1, 256);
            Assert.InRange(new FileInfo(path + ".1").Length, 1, 256);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
