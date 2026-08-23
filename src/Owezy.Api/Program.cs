using Owezy.Api;
using Owezy.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOwezyApplication(builder.Configuration);
builder.Services.AddOwezyInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapOtpEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
