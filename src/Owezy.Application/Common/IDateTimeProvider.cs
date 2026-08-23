namespace Owezy.Application.Common;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
