using System.Diagnostics;
using System.Threading.RateLimiting;
using EthioServe.API.Authorization;
using EthioServe.API.Configuration;
using EthioServe.API.Middleware;
using EthioServe.API.Services;
using EthioServe.Application;
using EthioServe.Application.Abstractions;
using EthioServe.Domain.Common;
using EthioServe.Infrastructure;
using EthioServe.Infrastructure.Identity;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Web
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Authentication: validated with the same rules the token tests use. Settings come from DI, so they are
// read when the first request arrives (this also makes them overridable in tests).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false; // keep "sub" and "role" as they are in the token
        bearer.TokenValidationParameters = JwtTokenValidation.CreateParameters(jwt.Value);
    });

// Authorization: deny by default. Anything without an explicit attribute requires a logged-in user.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.AdminOnly, policy => policy.RequireRole(Roles.Admin))
    .AddPolicy(Policies.ProviderOnly, policy => policy.RequireRole(Roles.Provider))
    .AddPolicy(Policies.CustomerOnly, policy => policy.RequireRole(Roles.Customer));

// CORS: only the origins listed in configuration
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Rate limits per client IP. Limits are read per request from settings, so tests can lower them.
builder.Services.AddOptions<RateLimitSettings>()
    .Bind(builder.Configuration.GetSection(RateLimitSettings.SectionName));

static string ClientKey(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.GlobalPermitsPerMinute,
            Window = TimeSpan.FromMinutes(1)
        });
    });

    options.AddPolicy(RateLimitPolicies.Auth, context =>
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter($"auth:{ClientKey(context)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.AuthPermitsPerMinute,
            Window = TimeSpan.FromMinutes(1)
        });
    });
});

// Health checks
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();


// Order matters: request logging must wrap the exception handler so it logs the FINAL status code.
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}
else
{
    app.UseHsts();
}
if (app.Environment.IsDevelopment())
{
    // Development convenience: apply migrations and seed reference data. Production uses an explicit migration step.
    await app.Services.InitializeDatabaseAsync();
    await AdminBootstrapper.EnsureAdminAsync(app.Services, app.Configuration);
}
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/live", new() { Predicate = _ => false }).AllowAnonymous();

app.Run();

// Required so integration tests can reference the entry point.
public partial class Program;