namespace Rabbitmq.Tools
{
    public class Settings
    {
        public string HostName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string QueueName { get; set; }
        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
        public int MessagesCount { get; set; }
        public double PublishConfirmTimeout { get; set; }
        public int SleepTimeBetweenPublishes { get; set; }
    }
}
