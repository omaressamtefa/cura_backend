using Microsoft.Extensions.Logging;
using System;

namespace AuthApi;

public class FileLogger<T> : ILogger<T>
{
    private readonly string _filePath;

    public FileLogger(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    // Explicit interface implementation for BeginScope
    IDisposable? ILogger.BeginScope<TState>(TState state)
    {
        return new DummyDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (formatter == null)
        {
            throw new ArgumentNullException(nameof(formatter));
        }

        string message = formatter(state, exception);
        if (message != null)
        {
            File.AppendAllText(_filePath, $"{DateTime.UtcNow}: {logLevel}: {message}{Environment.NewLine}");
        }
    }
}

public class DummyDisposable : IDisposable
{
    public void Dispose()
    {
        // No-op
    }
}