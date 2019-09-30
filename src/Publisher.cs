using RabbitMQ.Client;
using ShellProgressBar;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Rabbitmq.Tools
{
    public class Publisher
    {
        private ConcurrentDictionary<ulong, string> outstandingConfirms;
        private int ackCount;
        private int nackCount;
        private readonly Settings settings;

        public Publisher(Settings settings)
        {
            this.settings = settings;
        }

        public Task Run(CancellationToken cancellationToken)
        {
            Task task = null;

            task = Task.Run(() =>
            {
                Publish(task, cancellationToken);
            });

            return task;
        }
        private void Publish(Task task, CancellationToken cancellationToken)
        {
            var options = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true,
                CollapseWhenFinished = true
            };

            using (var pbar = new ProgressBar(settings.MessagesCount + 1,
                "Starting to publish messages...", options))
            {
                //Console.WriteLine("Starting to publish messages...");

                ackCount = 0;
                nackCount = 0;
                outstandingConfirms = new ConcurrentDictionary<ulong, string>();

                pbar.Tick("Connecting to RabbitMQ...");

                var factory = new ConnectionFactory() { HostName = settings.HostName, UserName = settings.UserName, Password = settings.Password };
                using (var connection = factory.CreateConnection())
                using (var channel = connection.CreateModel())
                {
                    channel.ConfirmSelect();

                    channel.BasicAcks += (sender, ea) =>
                    {
                        ackCount += CountConfirms(ea.DeliveryTag, ea.Multiple);
                    };

                    channel.BasicNacks += (sender, ea) =>
                    {
                        nackCount += CountConfirms(ea.DeliveryTag, ea.Multiple);
                    };

                    channel.QueueDeclare(queue: settings.QueueName,
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    for (int i = 0; i < settings.MessagesCount; i++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        string message = $"Message {i}";
                        var body = Encoding.UTF8.GetBytes(message);
                        outstandingConfirms.TryAdd(channel.NextPublishSeqNo, message);
                        channel.BasicPublish(exchange: settings.ExchangeName,
                                             routingKey: settings.RoutingKey,
                                             basicProperties: null,
                                             body: body);

                        pbar.Tick($"Publishing message: {message}...");
                        //Console.WriteLine($"Publishing message: {message}");

                        Thread.Sleep(settings.SleepTimeBetweenPublishes);
                    }

                    channel.WaitForConfirmsOrDie(TimeSpan.FromMilliseconds(settings.PublishConfirmTimeout));

                    pbar.Tick(pbar.MaxTicks, $"Finished, ACK {ackCount}, NACK {nackCount}...");
                }

                //Console.WriteLine("Messages published...");

                //Console.WriteLine("- Messages ACK {0}", ackCount);
                //Console.WriteLine("- Messages NACK {0}", nackCount);

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException(task);
            }
        }

        private int CountConfirms(ulong sequenceNumber, bool multiple)
        {
            if (multiple)
            {
                var confirmed = outstandingConfirms.Where(k => k.Key <= sequenceNumber);
                var count = confirmed.Count();

                foreach (var entry in confirmed)
                {
                    outstandingConfirms.TryRemove(entry.Key, out _);
                }

                return count;
            }
            else
            {
                outstandingConfirms.TryRemove(sequenceNumber, out _);

                return 1;
            }
        }
    }
}
