using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using Owezy.Api;
using Owezy.Api.Auth;
using Owezy.Api.Billing;
using Owezy.Api.Receipts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOwezyApplication(builder.Configuration);
builder.Services.AddOwezyInfrastructure(builder.Configuration);
builder.Services.AddOwezyAuthentication(builder.Configuration);
builder.Services.AddHealthChecks();

// Use HTML-safe JSON encoding (escapes <, >, &, ', ") as a defense-in-depth
// measure against XSS when OCR-derived or user-supplied content appears in responses.
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Encoder = JavaScriptEncoder.Default;
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var clientPath = Path.Combine(builder.Environment.ContentRootPath, "..", "Owezy.Client");
if (Directory.Exists(clientPath))
{
    var fileProvider = new PhysicalFileProvider(Path.GetFullPath(clientPath));
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapHealthChecks("/health");
app.MapOtpEndpoints();
app.MapBillEndpoints();
app.MapReceiptEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
