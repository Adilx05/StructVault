namespace StructVault.Application.Abstractions.Logging;

public sealed class ApplicationLogEntry
{
    public ApplicationLogEntry(
        DateTimeOffset occurredAtUtc,
        ApplicationLogLevel level,
        string category,
        string eventName,
        string? detail)
    {
        if (!Enum.IsDefined(typeof(ApplicationLogLevel), level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Application log level is not supported.");
        }

        if (occurredAtUtc == default)
        {
            throw new ArgumentException("Application log timestamp must be specified.", nameof(occurredAtUtc));
        }

        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Level = level;
        Category = RequireNonEmpty(category, nameof(category));
        EventName = RequireNonEmpty(eventName, nameof(eventName));
        Detail = NormalizeOptional(detail);
    }

    public DateTimeOffset OccurredAtUtc { get; }

    public ApplicationLogLevel Level { get; }

    public string Category { get; }

    public string EventName { get; }

    public string? Detail { get; }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        string normalizedValue = Normalize(value);
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", parameterName);
        }

        return normalizedValue;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        string normalizedValue = Normalize(value);
        return normalizedValue.Length == 0 ? null : normalizedValue;
    }

    private static string Normalize(string value)
    {
        return value.Trim()
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
    }
}
