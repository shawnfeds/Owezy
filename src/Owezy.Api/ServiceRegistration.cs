using Microsoft.EntityFrameworkCore;
using Owezy.Application.Auth;
using Owezy.Application.Common;
using Owezy.Infrastructure.Persistence;

namespace Owezy.Api;

public static class ServiceRegistration
{
    public static IServiceCollection AddOwezyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("OwezyDb");

        services.AddDbContext<OwezyDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IOtpChallengeRepository, SqlOtpChallengeRepository>();

        return services;
    }

    public static IServiceCollection AddOwezyApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Date/time
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        // OTP hashing — HMAC secret from configuration
        var otpHasherOptions = configuration
            .GetSection(OtpHasherOptions.Position)
            .Get<OtpHasherOptions>() ?? new OtpHasherOptions();

        services.AddSingleton<IOtpHasher>(_ => new HmacSha256OtpHasher(otpHasherOptions));
        services.AddSingleton<IOtpGenerator, SecureOtpGenerator>();

        // SMS provider: development only for this milestone
        services.AddSingleton<ISmsProvider, DevelopmentSmsProvider>();

        // Application service
        services.AddScoped<IOtpService, OtpService>();

        return services;
    }
}
