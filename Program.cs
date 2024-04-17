using GraphQL.AspNet.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using OpenTelemetry.ResourceDetectors.Container;
using OpenTelemetry.ResourceDetectors.Host;
using OpenTelemetry.Logs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;


var builder = WebApplication.CreateBuilder(args);

///////// BEGIN PROMETHEUS METRICS

Action<ResourceBuilder> appResourceBuilder = resource =>
    resource.AddDetector(new ContainerResourceDetector()).AddDetector(new HostDetector());


builder.Services.AddOpenTelemetry().ConfigureResource(appResourceBuilder).WithTracing(tracerBuilder =>
    tracerBuilder.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddGrpcClientInstrumentation()
        .AddOtlpExporter());

builder.Services.AddOpenTelemetry().ConfigureResource(appResourceBuilder).WithMetrics(meterBuilder =>
    meterBuilder.AddProcessInstrumentation().AddRuntimeInstrumentation().AddAspNetCoreInstrumentation()
        .AddOtlpExporter().AddPrometheusExporter());

builder.Logging.AddOpenTelemetry(options => options.AddOtlpExporter());

///////// END PROMETHEUS METRICS


///////// BEGIN AUTH

// Needed If Running In A Docker Container
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo("temp-keys")).UseCryptographicAlgorithms(
    new AuthenticatedEncryptorConfiguration
    {
        EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
        ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
    });

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
        {
            options.Cookie.Name = "AuthCookie";
            // Change This As Required
            options.ExpireTimeSpan = TimeSpan.FromHours(3);
            // This Cookies Is Required For Any Kind Of Authentication
            options.Cookie.IsEssential = true;
            options.LoginPath = "/Login";
            options.LogoutPath = "/Logout";
            options.AccessDeniedPath = "/Error";
        }
    );

builder.Services.AddAuthorizationBuilder()
    // Allows Marking Entire Pages As AdminOnly
    .AddPolicy("AdminOnly", policy =>
    {
        policy.RequireRole("Admin");
        policy.RequireAuthenticatedUser();
    });

builder.Services.AddSession(options =>
{
    // Change This As Required
    options.IdleTimeout = TimeSpan.FromHours(3);
    options.Cookie.IsEssential = true;
    // Change This To Match The Actual Domain
    options.Cookie.Domain = "gauss-gymnasium.de/gawo";
});

///////// END AUTH

builder.Services.AddRazorPages();

builder.Services.AddGraphQL(options =>
{
    options.AuthorizationOptions.Method = GraphQL.AspNet.Security.AuthorizationMethod.PerRequest;
});

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.UseRouting();

app.MapGraphQLPlayground("/Playground");

app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.MapRazorPages();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseGraphQL();

await app.RunAsync();