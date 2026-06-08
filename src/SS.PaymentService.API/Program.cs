using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Infrastructure.Messaging;
using SS.PaymentService.API.Infrastructure.Gateway;
using SS.PaymentService.API.Features.Payments.InitiatePayment;
using SS.PaymentService.API.Features.Payments.GetPaymentByOrder;
using SS.PaymentService.API.Features.Payments.CancelPayment;
using SS.PaymentService.API.Features.Refunds.InitiateRefund;
using SS.PaymentService.API.Webhooks;
using SS.PaymentService.API.Extensions;
using SS.PaymentService.API.Middleware;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));

// Add Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register DbContext with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (builder.Environment.IsEnvironment("Testing"))
{
    // Skip db initialization or let test host override
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(
        options => options.UseNpgsql(connectionString),
        contextLifetime: ServiceLifetime.Scoped,
        optionsLifetime: ServiceLifetime.Singleton);

    builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Background workers
    builder.Services.AddHostedService<OutboxWorker>();
    builder.Services.AddHostedService<OrderEventConsumerWorker>();
}

// Midtrans configuration
builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection(MidtransOptions.SectionName));
builder.Services.AddHttpClient<MidtransClient>()
    .AddStandardResilienceHandler();

// Register Observability & OpenTelemetry
builder.Services.AddPaymentObservability(builder.Configuration);

// Register MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Register FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Enforce OWASP security response headers
app.UseMiddleware<SecurityHeadersMiddleware>();

// Enforce Zero-Trust HMAC Origin Verification (unless testing)
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseMiddleware<GatewaySignatureMiddleware>();
}

// Map Vertical Slice Endpoints
app.MapInitiatePaymentEndpoint();
app.MapGetPaymentByOrderEndpoint();
app.MapCancelPaymentEndpoint();
app.MapInitiateRefundEndpoint();
app.MapMidtransWebhookEndpoint();

// Health Check Endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "PaymentService" }));

app.Run();

// Make the implicit Program class public so functional test projects can reference it
public partial class Program { }
