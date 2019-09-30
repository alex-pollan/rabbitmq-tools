using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ShellProgressBar;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rabbitmq.Tools
{
    public class Consumer
    {
        private Settings settings;
        private int receivedCount;

        public Consumer(Settings settings)
        {
            this.settings = settings;
        }

        public Task Run(CancellationToken cancellationToken)
        {
            Task task = null;

            task = Task.Run(() =>
            {
                Consume(task, cancellationToken);
            });

            return task;
        }

        private void Consume(Task task, CancellationToken cancellationToken)
        {
            var options = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true
            };

            using (var pbar = new ProgressBar(100, "Starting to consume messages...", options))
            {
                //Console.WriteLine("Starting to consume messages...");

                receivedCount = 0;

                pbar.Tick("Connecting to RabbitMQ...");

                var factory = new ConnectionFactory { UserName = settings.UserName, Password = settings.Password };
                using (var connection = factory.CreateConnection(settings.HostName.Split(",", StringSplitOptions.RemoveEmptyEntries)))
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: settings.QueueName,
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    //channel.QueueBind(settings.QueueName, settings.ExchangeName, settings.RoutingKey);

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (model, ea) =>
                    {
                        try
                        {
                            pbar.Tick(receivedCount % 100, $"Consuming message: {Encoding.UTF8.GetString(ea.Body)}...");
                            //Console.WriteLine($"Consuming message: {Encoding.UTF8.GetString(ea.Body)}");
                        }
                        catch (Exception)
                        {
                            //ignore
                        }

                        receivedCount++;
                    };

                    channel.BasicConsume(queue: settings.QueueName,
                                         autoAck: true,
                                         consumer: consumer);

                    pbar.Tick("Waiting for messages...");

                    while (!cancellationToken.IsCancellationRequested)
                        Thread.Sleep(10);

                    pbar.Tick(pbar.MaxTicks, $"Finished, messages received {receivedCount}");
                }

                //Console.WriteLine($"- Messages received {receivedCount}");
                                
                throw new TaskCanceledException(task);
            }
        }
    }
}