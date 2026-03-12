using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Chronos.MainApi.Schedule.Messaging;

public class MessagePublisher(
    IRabbitMqConnectionFactory connectionFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<MessagePublisher> logger
) : IMessagePublisher
{
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<MessagePublisher> _logger = logger;

    public Task PublishAsync<T>(T message, string routingKey)
        where T : class
    {
        _logger.LogDebug(
            "Publishing message of type {MessageType} with routing key {RoutingKey}",
            typeof(T).Name,
            routingKey
        );

        try
        {
            using var channel = _connectionFactory.CreateChannel();

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            _logger.LogTrace("Serialized message to {ByteCount} bytes", body.Length);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                basicProperties: properties,
                body: body
            );

            _logger.LogDebug(
                "Published message of type {MessageType} to exchange {Exchange} with routing key {RoutingKey}",
                typeof(T).Name,
                _options.ExchangeName,
                routingKey
            );

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType}", typeof(T).Name);
            throw;
        }
    }
}
