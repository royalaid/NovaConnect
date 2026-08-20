using System.Text;

namespace NovaConnect.App;

internal sealed class FileLogger
{
    private const long DefaultMaxBytes = 1_048_576;
    private readonly object _gate = new();
    private readonly string _path;
    private readonly long _maxBytes;

    internal FileLogger(string path, long maxBytes = DefaultMaxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);
        _path = path;
        _maxBytes = maxBytes;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    internal void Info(string message) => Write("INFO", message);
    internal void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        string line = $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            long existingBytes = File.Exists(_path) ? new FileInfo(_path).Length : 0;
            long incomingBytes = Encoding.UTF8.GetByteCount(line);
            if (existingBytes > 0 && existingBytes + incomingBytes > _maxBytes)
            {
                File.Move(_path, _path + ".1", overwrite: true);
            }

            File.AppendAllText(_path, line);
        }
    }
}
