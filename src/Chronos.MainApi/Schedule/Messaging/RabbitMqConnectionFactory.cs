using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Chronos.MainApi.Schedule.Messaging;

public class RabbitMqConnectionFactory(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnectionFactory> logger
) : IRabbitMqConnectionFactory, IDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqConnectionFactory> _logger = logger;
    private IConnection? _connection;
    private readonly object _lock = new();

    public IConnection CreateConnection()
    {
        if (_connection != null && _connection.IsOpen)
        {
            _logger.LogTrace("Reusing existing RabbitMQ connection");
            return _connection;
        }

        lock (_lock)
        {
            if (_connection != null && _connection.IsOpen)
            {
                _logger.LogTrace("Reusing existing RabbitMQ connection (after lock)");
                return _connection;
            }

            _logger.LogInformation(
                "Creating RabbitMQ connection to {HostName}:{Port}",
                _options.HostName,
                _options.Port
            );

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
            };

            _connection = factory.CreateConnection();

            _logger.LogInformation("RabbitMQ connection established successfully");

            return _connection;
        }
    }

    public IModel CreateChannel()
    {
        _logger.LogDebug("Creating new RabbitMQ channel");
        var connection = CreateConnection();
        var channel = connection.CreateModel();

        _logger.LogTrace("Declaring exchange and queues");

        // Declare exchange
        channel.ExchangeDeclare(
            exchange: _options.ExchangeName,
            type: "topic",
            durable: true,
            autoDelete: false
        );

        // Declare batch queue
        channel.QueueDeclare(
            queue: _options.BatchQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        channel.QueueBind(
            queue: _options.BatchQueueName,
            exchange: _options.ExchangeName,
            routingKey: "request.batch"
        );

        // Declare online queue
        channel.QueueDeclare(
            queue: _options.OnlineQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        channel.QueueBind(
            queue: _options.OnlineQueueName,
            exchange: _options.ExchangeName,
            routingKey: "request.online"
        );

        _logger.LogDebug("RabbitMQ channel created with queues configured");

        return channel;
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
