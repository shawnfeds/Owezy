using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Owezy.Application.Auth;
using Owezy.Application.Billing;
using Owezy.Application.Common;
using Owezy.Application.Receipts;
using Owezy.Infrastructure.Ocr;
using Owezy.Infrastructure.Persistence;
using Owezy.Infrastructure.Storage;

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
        services.AddScoped<IBillRepository, SqlBillRepository>();
        services.AddScoped<IReceiptRepository, SqlReceiptRepository>();
        services.AddSingleton<IReceiptStorage, LocalFileReceiptStorage>();
        services.AddSingleton<IOcrService, TesseractOcrService>();

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

        // JWT Access Token options & service
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Position));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();

        // SMS provider: development only for this milestone
        services.AddSingleton<ISmsProvider, DevelopmentSmsProvider>();

        // Participant Token Generator
        services.AddSingleton<IParticipantTokenGenerator, Owezy.Infrastructure.Security.CryptoParticipantTokenGenerator>();

        // Application services
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IBillService, BillService>();
        services.AddScoped<IReceiptService, ReceiptService>();

        return services;
    }

    public static IServiceCollection AddOwezyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorization();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptionsAcc) =>
            {
                var jwtOptions = jwtOptionsAcc.Value;

                if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || jwtOptions.SigningKey.Length < 32)
                {
                    throw new InvalidOperationException(
                        "JWT SigningKey is missing or too short. Provide a key of at least 32 characters via configuration.");
                }

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        return services;
    }
}
