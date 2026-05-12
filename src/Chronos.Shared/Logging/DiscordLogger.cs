using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Chronos.Shared.Logging;

/// <summary>
/// Logger that batches logs and sends them to Discord via webhooks
/// </summary>
public class DiscordLogger : ILogger
{
    private readonly string _categoryName;
    private readonly DiscordLoggerConfiguration _config;
    private readonly DiscordLoggerProvider _provider;

    public DiscordLogger(string categoryName, DiscordLoggerConfiguration config, DiscordLoggerProvider provider)
    {
        _categoryName = categoryName;
        _config = config;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => default;

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _config.MinimumLogLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var logEntry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            LogLevel = logLevel,
            Category = _categoryName,
            Message = message,
            Exception = exception
        };

        _provider.AddLog(logEntry);
    }
}

/// <summary>
/// Provides Discord loggers and manages batching and flushing
/// </summary>
public class DiscordLoggerProvider : ILoggerProvider
{
    private readonly DiscordLoggerConfiguration _config;
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private readonly HttpClient _httpClient;
    private readonly Timer _flushTimer;
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);
    private bool _disposed;

    public DiscordLoggerProvider(IOptions<DiscordLoggerConfiguration> config)
    {
        _config = config.Value;
        _httpClient = new HttpClient();
        
        // Set up periodic flushing
        _flushTimer = new Timer(
            async _ => await FlushLogsAsync(),
            null,
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds),
            TimeSpan.FromSeconds(_config.FlushIntervalSeconds));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DiscordLogger(categoryName, _config, this);
    }

    public void AddLog(LogEntry logEntry)
    {
        _logQueue.Enqueue(logEntry);
        
        // Write to console immediately
        WriteToConsole(logEntry);
    }

    private void WriteToConsole(LogEntry logEntry)
    {
        var color = logEntry.LogLevel switch
        {
            LogLevel.Trace => ConsoleColor.Gray,
            LogLevel.Debug => ConsoleColor.DarkGray,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        
        var timestamp = logEntry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        Console.WriteLine($"[{timestamp}] [{logEntry.LogLevel}] {logEntry.Category}: {logEntry.Message}");
        
        if (logEntry.Exception != null)
        {
            Console.WriteLine($"Exception: {logEntry.Exception}");
        }
        
        Console.ForegroundColor = originalColor;
    }

    public async Task FlushLogsAsync()
    {
        if (_disposed || _logQueue.IsEmpty)
        {
            return;
        }

        await _flushSemaphore.WaitAsync();
        try
        {
            var logs = new List<LogEntry>();
            while (_logQueue.TryDequeue(out var log))
            {
                logs.Add(log);
            }

            if (logs.Count == 0)
            {
                return;
            }

            // If Discord webhook is configured, send to Discord
            if (!string.IsNullOrWhiteSpace(_config.DiscordWebhookUrl))
            {
                await SendToDiscordAsync(logs);
            }
        }
        finally
        {
            _flushSemaphore.Release();
        }
    }

    private async Task SendToDiscordAsync(List<LogEntry> logs)
    {
        try
        {
            var messages = BuildDiscordMessages(logs);
            
            foreach (var message in messages)
            {
                var payload = new
                {
                    content = message,
                    username = _config.BotName
                };

                var response = await _httpClient.PostAsJsonAsync(_config.DiscordWebhookUrl, payload);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to send logs to Discord. Status: {response.StatusCode}, Error: {errorContent}");
                }
                
                // Discord rate limit: max 5 requests per 2 seconds per webhook
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending logs to Discord: {ex.Message}");
        }
    }

    private List<string> BuildDiscordMessages(List<LogEntry> logs)
    {
        var messages = new List<string>();
        var currentMessage = new StringBuilder();

        // Discord hard limit is 2000 chars per message. Code-fence wrapping adds
        // ```\n ... \n``` (8 chars). We use 1900 as the safe inner-content budget.
        const int maxLength = 1900;

        void FlushBuffer()
        {
            if (currentMessage.Length == 0) return;
            messages.Add($"```\n{currentMessage}\n```");
            currentMessage.Clear();
        }

        foreach (var log in logs)
        {
            var logLine = FormatLogEntry(log);

            // If the entry itself is bigger than a single message, split it
            // into chunks and emit each chunk as its own message so nothing
            // is dropped or truncated.
            if (logLine.Length > maxLength)
            {
                FlushBuffer();

                var totalChunks = (logLine.Length + maxLength - 1) / maxLength;
                for (var i = 0; i < totalChunks; i++)
                {
                    var start = i * maxLength;
                    var len = Math.Min(maxLength, logLine.Length - start);
                    var chunk = logLine.Substring(start, len);
                    messages.Add($"```\n[part {i + 1}/{totalChunks}]\n{chunk}\n```");
                }
                continue;
            }

            // +1 accounts for the newline AppendLine adds.
            if (currentMessage.Length + logLine.Length + 1 > maxLength)
            {
                FlushBuffer();
            }

            currentMessage.AppendLine(logLine);
        }

        FlushBuffer();

        return messages;
    }

    private string FormatLogEntry(LogEntry log)
    {
        var emoji = log.LogLevel switch
        {
            LogLevel.Trace => "🔍",
            LogLevel.Debug => "🐛",
            LogLevel.Information => "ℹ️",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            LogLevel.Critical => "🔥",
            _ => "📝"
        };

        var sb = new StringBuilder();
        sb.Append($"{emoji} [{log.Timestamp:yyyy-MM-dd HH:mm:ss}] [{log.LogLevel}] {log.Category}");
        sb.AppendLine();
        sb.Append($"   {log.Message}");
        
        if (log.Exception != null)
        {
            sb.AppendLine();
            sb.Append($"   Exception: {log.Exception}");
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        
        // Flush remaining logs before disposing
        FlushLogsAsync().GetAwaiter().GetResult();
        
        _flushTimer?.Dispose();
        _httpClient?.Dispose();
        _flushSemaphore?.Dispose();
    }
}

/// <summary>
/// Represents a single log entry
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
}
