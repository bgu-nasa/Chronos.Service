using System.Text;
using System.Text.Json;
using Chronos.Domain.Schedule.Messages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chronos.MainApi.Schedule.Messaging;

public class SchedulingResultNotificationConsumer(
    IRabbitMqConnectionFactory connectionFactory,
    IHubContext<SchedulingNotificationsHub> hubContext,
    IOptions<RabbitMqOptions> options,
    ILogger<SchedulingResultNotificationConsumer> logger
) : BackgroundService
{
    private readonly IRabbitMqConnectionFactory _connectionFactory = connectionFactory;
    private readonly IHubContext<SchedulingNotificationsHub> _hubContext = hubContext;
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<SchedulingResultNotificationConsumer> _logger = logger;
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = _connectionFactory.CreateChannel();
        _channel.ExchangeDeclare(_options.ExchangeName, "topic", durable: true, autoDelete: false);
        _channel.QueueDeclare(_options.ResultsQueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(_options.ResultsQueueName, _options.ExchangeName, routingKey: "result.#");
        _channel.BasicQos(0, 1, false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var result = JsonSerializer.Deserialize<SchedulingResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

                if (result?.InitiatedByUserId is { } userId)
                {
                    await _hubContext.Clients.User(userId.ToString()).SendAsync(
                        "SchedulingCompleted",
                        new
                        {
                            requestId = result.RequestId,
                            success = result.Success,
                            assignmentsCreated = result.AssignmentsCreated,
                            assignmentsModified = result.AssignmentsModified,
                            unscheduledActivityIds = result.UnscheduledActivityIds.Select(id => id.ToString()).ToArray(),
                            failureReason = result.FailureReason,
                        },
                        stoppingToken);
                }

                _channel!.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduling result notification");
                _channel!.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(_options.ResultsQueueName, autoAck: false, consumer);
        _logger.LogInformation("Scheduling result consumer listening on {Queue}", _options.ResultsQueueName);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
