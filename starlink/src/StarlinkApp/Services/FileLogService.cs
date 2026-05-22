using System.Globalization;
using System.IO;

namespace StarlinkApp.Services;

public interface IAppLogService
{
    void Write(string eventName, string message);
}

public sealed class FileLogService : IAppLogService
{
    private readonly string _logDirectory;

    public FileLogService(string runtimeRoot)
    {
        _logDirectory = Path.Combine(runtimeRoot, "logs");
        Directory.CreateDirectory(_logDirectory);
    }

    public static FileLogService CreateDefault()
    {
        return new FileLogService(AppContext.BaseDirectory);
    }

    public void Write(string eventName, string message)
    {
        var logPath = Path.Combine(_logDirectory, $"starlink_{DateTime.Now:yyyy-MM-dd}.log");
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:O} [{eventName}] {message}{Environment.NewLine}");

        File.AppendAllText(logPath, line);
    }
}
