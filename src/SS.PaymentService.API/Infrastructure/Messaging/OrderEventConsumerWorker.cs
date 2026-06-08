using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SS.PaymentService.API.Domain.Entities;
using SS.PaymentService.API.Domain.Enums;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Infrastructure.Gateway;

namespace SS.PaymentService.API.Infrastructure.Messaging;

public class OrderEventConsumerWorker : BackgroundService
{
    private readonly ILogger<OrderEventConsumerWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IServiceProvider _serviceProvider;
    private IConnection? _connection;
    private IChannel? _channel;
    private const string ExchangeName = "samstore.events";
    private const string QueueName = "ss-payment-service.order-events";
    private const string RoutingKey = "order.created";

    public OrderEventConsumerWorker(
        ILogger<OrderEventConsumerWorker> logger,
        IConfiguration configuration,
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _dbContextFactory = dbContextFactory;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Event Consumer Worker starting.");

        await Task.Delay(1000, stoppingToken); // Give time for app to fully start

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await InitializeRabbitMQAsync(stoppingToken);
                await ConsumeMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OrderEventConsumerWorker. Retrying in 5 seconds.");
                await CleanupResourcesAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        await CleanupResourcesAsync();
        _logger.LogInformation("Order Event Consumer Worker stopped.");
    }

    private async Task InitializeRabbitMQAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.TryParse(_configuration["RabbitMQ:Port"], out var port) ? port : 5672,
            UserName = _configuration["RabbitMQ:Username"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port} for order event consumption", factory.HostName, factory.Port);

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare exchange
        await _channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        // Declare queue
        await _channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // Bind queue
        await _channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: RoutingKey,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Bound queue {Queue} to exchange {Exchange} with routing key {RoutingKey}", QueueName, ExchangeName, RoutingKey);
    }

    private async Task ConsumeMessagesAsync(CancellationToken stoppingToken)
    {
        if (_channel == null)
            throw new InvalidOperationException("RabbitMQ channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                var messageJson = Encoding.UTF8.GetString(body);

                await ProcessMessageAsync(messageJson, ea, stoppingToken);

                await _channel.BasicAckAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing order event message");
                await _channel.BasicNackAsync(
                    deliveryTag: ea.DeliveryTag,
                    multiple: false,
                    requeue: true,
                    cancellationToken: stoppingToken);
            }
        };

        await _channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation("Started consuming from queue {Queue}", QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessageAsync(string jsonMessage, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Received order event: {Message}", jsonMessage);

        using var doc = JsonDocument.Parse(jsonMessage);
        var root = doc.RootElement;

        if (!root.TryGetProperty("orderPublicId", out var orderPublicIdProp) ||
            !root.TryGetProperty("userId", out var userIdProp) ||
            !root.TryGetProperty("userPublicId", out var userPublicIdProp) ||
            !root.TryGetProperty("totalAmount", out var totalAmountProp))
        {
            _logger.LogWarning("Message missing required properties: {Message}", jsonMessage);
            return;
        }

        var orderPublicId = orderPublicIdProp.GetGuid();
        var userId = userIdProp.GetInt32();
        var userPublicId = userPublicIdProp.GetGuid();
        var totalAmount = totalAmountProp.GetDecimal();
        var currency = root.TryGetProperty("currencyCode", out var currencyProp) ? currencyProp.GetString() ?? "IDR" : "IDR";

        var messageId = GenerateMessageId(ea, jsonMessage);

        await using var dbContext = _dbContextFactory.CreateDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);

        try
        {
            var alreadyProcessed = await dbContext.InboxEvents.AnyAsync(ie => ie.MessageId == messageId, stoppingToken);
            if (alreadyProcessed)
            {
                _logger.LogInformation("Message {MessageId} already processed. Skipping.", messageId);
                await transaction.CommitAsync(stoppingToken);
                return;
            }

            var paymentExists = await dbContext.Payments.AnyAsync(p => p.OrderPublicId == orderPublicId, stoppingToken);
            if (!paymentExists)
            {
                var payment = new Payment
                {
                    OrderPublicId = orderPublicId,
                    UserId = userId,
                    UserPublicId = userPublicId,
                    Amount = totalAmount,
                    Currency = currency,
                    PaymentStatus = PaymentStatus.Pending,
                    CreatedBy = "OrderEventConsumer"
                };

                // Generate Midtrans Snap token
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var midtransClient = scope.ServiceProvider.GetRequiredService<MidtransClient>();
                    var snapRes = await midtransClient.CreateSnapTransactionAsync(payment.PublicId, payment.Amount, payment.Currency);
                    if (snapRes != null)
                    {
                        payment.SnapToken = snapRes.Token;
                        payment.SnapRedirectUrl = snapRes.RedirectUrl;
                        payment.PaymentStatus = PaymentStatus.WaitingPayment;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate Midtrans Snap Token for order {OrderPublicId}", orderPublicId);
                }

                dbContext.Payments.Add(payment);
                _logger.LogInformation("Initialized Payment for Order {OrderPublicId} with status {Status}", orderPublicId, payment.PaymentStatus);
            }

            var inboxEvent = new InboxEvent
            {
                MessageId = messageId,
                EventType = ea.RoutingKey,
                AggregateType = "Order",
                Payload = jsonMessage,
                Status = "PROCESSED",
                ProcessedAt = DateTimeOffset.UtcNow
            };

            dbContext.InboxEvents.Add(inboxEvent);
            await dbContext.SaveChangesAsync(stoppingToken);

            await transaction.CommitAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction failed for order event message {MessageId}", messageId);
            await transaction.RollbackAsync(stoppingToken);
            throw;
        }
    }

    private string GenerateMessageId(BasicDeliverEventArgs ea, string jsonMessage)
    {
        if (ea.BasicProperties.Headers != null &&
            ea.BasicProperties.Headers.TryGetValue("message-id", out var headerValue) &&
            headerValue is byte[] headerBytes &&
            headerBytes.Length > 0)
        {
            return Encoding.UTF8.GetString(headerBytes);
        }

        var input = $"{ea.RoutingKey}:{jsonMessage}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hashBytes);
    }

    private async Task CleanupResourcesAsync()
    {
        if (_channel != null && _channel.IsOpen)
        {
            await _channel.CloseAsync();
        }

        if (_connection != null && _connection.IsOpen)
        {
            await _connection.CloseAsync();
        }

        _channel = null;
        _connection = null;
    }
}
