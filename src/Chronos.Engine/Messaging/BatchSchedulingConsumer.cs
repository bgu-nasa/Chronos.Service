using System.Text;
using System.Text.Json;
using Chronos.Domain.Schedule.Messages;
using Chronos.Engine.Configuration;
using Chronos.Engine.Matching;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chronos.Engine.Messaging;

public class BatchSchedulingConsumer(
    IRabbitMqConnectionFactory connectionFactory,
    MatchingOrchestrator orchestrator,
    IMessagePublisher messagePublisher,
    IOptions<RabbitMqOptions> options,
    ILogger<BatchSchedulingConsumer> logger
) : BackgroundService
{
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly MatchingOrchestrator _orchestrator = orchestrator;
    private readonly IMessagePublisher _messagePublisher = messagePublisher;
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<BatchSchedulingConsumer> _logger = logger;
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Batch Scheduling Consumer starting...");

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
                        "Received batch scheduling request. DeliveryTag: {DeliveryTag}",
                        ea.DeliveryTag
                    );

                    var request = JsonSerializer.Deserialize<SchedulePeriodRequest>(message);

                    if (request == null)
                    {
                        _logger.LogError("Failed to deserialize SchedulePeriodRequest");
                        _channel.BasicNack(ea.DeliveryTag, false, false);
                        return;
                    }

                    // Execute batch scheduling
                    var result = await _orchestrator.ExecuteAsync(
                        request,
                        SchedulingMode.Batch,
                        stoppingToken
                    );

                    // Publish result
                    await _messagePublisher.PublishAsync(
                        result,
                        result.Success ? "result.success" : "result.failed"
                    );

                    _channel.BasicAck(ea.DeliveryTag, false);

                    _logger.LogInformation(
                        "Batch scheduling completed. Success: {Success}, Assignments: {Assignments}",
                        result.Success,
                        result.AssignmentsCreated
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing batch scheduling request");
                    _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue
                }
            };

            _channel.BasicConsume(
                queue: _options.BatchQueueName,
                autoAck: false,
                consumer: consumer
            );

            _logger.LogInformation(
                "Batch Scheduling Consumer started, listening to queue: {QueueName}",
                _options.BatchQueueName
            );

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch Scheduling Consumer error");
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
