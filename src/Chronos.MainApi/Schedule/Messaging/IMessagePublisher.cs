namespace Chronos.MainApi.Schedule.Messaging;

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string routingKey)
        where T : class;
}
