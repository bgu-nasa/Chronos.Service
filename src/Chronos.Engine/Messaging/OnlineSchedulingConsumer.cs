using System.Text;
using System.Text.Json;
using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Configuration;
using Chronos.Engine.Matching;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chronos.Engine.Messaging;

public class OnlineSchedulingConsumer(
    IRabbitMqConnectionFactory connectionFactory,
    MatchingOrchestrator orchestrator,
    IMessagePublisher messagePublisher,
    IOptions<RabbitMqOptions> options,
    ILogger<OnlineSchedulingConsumer> logger
) : BackgroundService
{
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly MatchingOrchestrator _orchestrator = orchestrator;
    private readonly IMessagePublisher _messagePublisher = messagePublisher;
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<OnlineSchedulingConsumer> _logger = logger;
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Online Scheduling Consumer starting...");

        // Retry connection with exponential backoff
        const int maxRetries = 10;
        const int initialDelayMs = 2000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _channel = _connectionFactory.CreateChannel();
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
                break; // Success, exit retry loop
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                var delay = initialDelayMs * attempt; // Exponential backoff: 2s, 4s, 6s, etc.
                _logger.LogWarning(
                    "Failed to connect to RabbitMQ (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}ms. Error: {Error}",
                    attempt, maxRetries, delay, ex.Message);
                
                await Task.Delay(delay, stoppingToken);
            }
        }

        if (_channel == null)
        {
            _logger.LogError("Failed to connect to RabbitMQ after {MaxRetries} attempts", maxRetries);
            throw new InvalidOperationException("Unable to establish RabbitMQ connection after multiple retries");
        }

        try
        {

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    _logger.LogInformation(
                        "Received online scheduling request. DeliveryTag: {DeliveryTag}",
                        ea.DeliveryTag
                    );

                    var request = JsonSerializer.Deserialize<HandleConstraintChangeRequest>(
                        message
                    );

                    if (request == null)
                    {
                        _logger.LogError("Failed to deserialize HandleConstraintChangeRequest");
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                        return;
                    }

                    // Execute online scheduling
                    var result = await _orchestrator.ExecuteAsync(
                        request,
                        SchedulingMode.Online,
                        stoppingToken
                    );

                    // Publish result
                    await _messagePublisher.PublishAsync(
                        result,
                        result.Success ? "result.success" : "result.failed"
                    );

                    _channel.BasicAck(ea.DeliveryTag, false);

                    _logger.LogInformation(
                        "Online scheduling completed. Success: {Success}, Modified: {Modified}",
                        result.Success,
                        result.AssignmentsModified
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing online scheduling request");
                    _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue
                }
            };

            _channel.BasicConsume(
                queue: _options.OnlineQueueName,
                autoAck: false,
                consumer: consumer
            );

            _logger.LogInformation(
                "Online Scheduling Consumer started, listening to queue: {QueueName}",
                _options.OnlineQueueName
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Online Scheduling Consumer error");
            throw;
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
