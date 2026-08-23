using System.Collections.Concurrent;
using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public sealed class DevelopmentSmsProvider : ISmsProvider
{
    private readonly ConcurrentBag<(PhoneNumber Recipient, string Message, DateTimeOffset SentAt)> _sentMessages = new();

    public Task SendSmsAsync(PhoneNumber recipient, string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("SMS message content cannot be empty.", nameof(message));
        }

        _sentMessages.Add((recipient, message, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<(PhoneNumber Recipient, string Message, DateTimeOffset SentAt)> GetSentMessages()
    {
        return _sentMessages.ToArray();
    }
}
