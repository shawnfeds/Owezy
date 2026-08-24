using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Owezy.Application.Auth;
using Owezy.Application.Billing;
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
        services.AddScoped<IBillRepository, SqlBillRepository>();

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
        var jwtOptions = configuration
            .GetSection(JwtOptions.Position)
            .Get<JwtOptions>() ?? new JwtOptions();

        services.AddSingleton(jwtOptions);
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();

        // SMS provider: development only for this milestone
        services.AddSingleton<ISmsProvider, DevelopmentSmsProvider>();

        // Application services
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IBillService, BillService>();

        return services;
    }

    public static IServiceCollection AddOwezyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorization();

        var jwtOptions = configuration
            .GetSection(JwtOptions.Position)
            .Get<JwtOptions>();

        var signingKey = jwtOptions?.SigningKey;
        if (!string.IsNullOrWhiteSpace(signingKey) && signingKey.Length >= 32)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions!.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                        ClockSkew = TimeSpan.Zero
                    };
                });
        }
        else
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();
        }

        return services;
    }
}
