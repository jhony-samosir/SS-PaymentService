using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SS.PaymentService.API.Infrastructure.Data;

namespace SS.PaymentService.API.Infrastructure.Messaging;

public class OutboxWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxWorker> _logger;

    public OutboxWorker(IServiceProvider serviceProvider, ILogger<OutboxWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEvents(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("OutboxWorker stopped.");
    }

    private async Task ProcessPendingEvents(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMQPublisher>();

        // Fetch pending outbox events using FOR UPDATE SKIP LOCKED to prevent concurrency issues
        var pendingEvents = await dbContext.OutboxEvents
            .FromSqlRaw("SELECT * FROM outbox_events WHERE status = 'PENDING' AND retry_count < 5 ORDER BY created_at ASC LIMIT 50 FOR UPDATE SKIP LOCKED")
            .ToListAsync(stoppingToken);

        if (!pendingEvents.Any())
        {
            return;
        }

        foreach (var evt in pendingEvents)
        {
            try
            {
                var routingKey = evt.EventType.ToLower();

                // Deserialize payload JSON string back to an object for publishing
                var payloadObj = JsonSerializer.Deserialize<object>(evt.Payload);

                var messageId = evt.PublicId.ToString();
                var correlationId = evt.AggregateId;

                await publisher.PublishAsync(routingKey, payloadObj, evt.EventType, messageId, correlationId);

                evt.Status = "PUBLISHED";
                evt.PublishedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox event {EventId}", evt.Id);
                evt.RetryCount++;
                evt.ErrorMessage = ex.Message;
                if (evt.RetryCount >= 5)
                {
                    evt.Status = "FAILED";
                }
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
