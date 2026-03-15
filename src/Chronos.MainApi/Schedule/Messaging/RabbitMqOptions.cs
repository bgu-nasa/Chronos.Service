namespace Chronos.MainApi.Schedule.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string BatchQueueName { get; set; } = "chronos.scheduling.batch";
    public string OnlineQueueName { get; set; } = "chronos.scheduling.online";
    public string ExchangeName { get; set; } = "chronos.scheduling";
}
