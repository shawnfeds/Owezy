using Owezy.Domain.Auth;

namespace Owezy.Application.Auth;

public interface ISmsProvider
{
    Task SendSmsAsync(PhoneNumber recipient, string message, CancellationToken cancellationToken = default);
}
