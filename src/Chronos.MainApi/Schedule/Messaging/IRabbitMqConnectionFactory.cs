using RabbitMQ.Client;

namespace Chronos.MainApi.Schedule.Messaging;

public interface IRabbitMqConnectionFactory
{
    IConnection CreateConnection();
    IModel CreateChannel();
}
