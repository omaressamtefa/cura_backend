using Microsoft.Extensions.Logging;

namespace AuthApi.Logging;

[ProviderAlias("File")]
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;

    public FileLoggerProvider(string logFilePath)
    {
        _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
    }

    public ILogger CreateLogger(string categoryName)
    {
        // Specify the type argument for FileLogger<T> as object
        return new FileLogger<object>(_logFilePath);
    }

    public void Dispose()
    {
        // No-op
    }
}