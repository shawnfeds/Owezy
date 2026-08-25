using Owezy.Api;
using Owezy.Api.Auth;
using Owezy.Api.Billing;
using Owezy.Api.Receipts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOwezyApplication(builder.Configuration);
builder.Services.AddOwezyInfrastructure(builder.Configuration);
builder.Services.AddOwezyAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapOtpEndpoints();
app.MapBillEndpoints();
app.MapReceiptEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
