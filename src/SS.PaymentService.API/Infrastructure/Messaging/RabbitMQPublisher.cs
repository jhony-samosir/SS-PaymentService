using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SS.PaymentService.API.Infrastructure.Messaging;

public interface IRabbitMQPublisher
{
    Task PublishAsync<T>(string routingKey, T message, string eventType, string? messageId = null, string? correlationId = null);
}

public sealed class RabbitMQPublisher : IRabbitMQPublisher, IAsyncDisposable
{
    private readonly string _hostName;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly ILogger<RabbitMQPublisher> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly string _exchangeName = "samstore.payment";
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public RabbitMQPublisher(IConfiguration configuration, ILogger<RabbitMQPublisher> logger)
    {
        _hostName = configuration["RabbitMQ:Host"] ?? "localhost";
        _port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;
        _username = configuration["RabbitMQ:Username"] ?? "guest";
        _password = configuration["RabbitMQ:Password"] ?? "guest";
        _logger = logger;
    }

    private async Task EnsureConnectionAsync()
    {
        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            {
                return;
            }

            var factory = new ConnectionFactory
            {
                HostName = _hostName,
                Port = _port,
                UserName = _username,
                Password = _password,
                AutomaticRecoveryEnabled = true
            };

            _logger.LogInformation("Connecting to RabbitMQ at {Host}:{Port}...", _hostName, _port);
            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            await _channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: "topic",
                durable: true,
                autoDelete: false);

            _logger.LogInformation("Connected to RabbitMQ and declared exchange {ExchangeName}", _exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task PublishAsync<T>(string routingKey, T message, string eventType, string? messageId = null, string? correlationId = null)
    {
        await EnsureConnectionAsync();

        var payload = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(payload);

        var headers = new System.Collections.Generic.Dictionary<string, object?>
        {
            { "event_type", eventType },
            { "service", "ss-payment-service" }
        };

        if (!string.IsNullOrEmpty(messageId))
        {
            headers.Add("message-id", messageId);
        }

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId,
            CorrelationId = correlationId,
            Headers = headers
        };

        await _channel!.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogDebug("Published event {EventType} to {ExchangeName} with routing key {RoutingKey}", eventType, _exchangeName, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null) await _channel.CloseAsync();
        if (_connection != null) await _connection.CloseAsync();
    }
}
