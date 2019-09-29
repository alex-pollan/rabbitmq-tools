using CommandLine;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rabbitmq.Tools
{
    class Program
    {
        private const string RunModePublish = "publish";
        private const string RunModeConsume = "consume";

        private static IConfigurationRoot configuration;
        private static Settings settings;

        static void Main(string[] args)
        {
            LoadSettings();

            Parser.Default.ParseArguments<Options>(args)
                   .WithParsed(o =>
                   {
                       Func<CancellationToken, Task> task = null;

                       if (RunModePublish.Equals(o.Mode, StringComparison.OrdinalIgnoreCase))
                       {
                           task = new Publisher(settings).Run;
                       }
                       else if (RunModeConsume.Equals(o.Mode, StringComparison.OrdinalIgnoreCase))
                       {
                           task = new Consumer(settings).Run;
                       }

                       ExecuteManuallyCancellableTaskAsync(task).Wait();

                       Console.WriteLine(" Press [enter] to exit.");
                   });


            Console.ReadLine();
        }

        public static async Task ExecuteManuallyCancellableTaskAsync(Func<CancellationToken, Task> longRunningCancellableOperation)
        {
            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                // Creating a task to listen to keyboard key press
                var keyBoardTask = Task.Run(() =>
                {
                    Console.WriteLine("Press enter to cancel");
                    Console.ReadKey();

                    // Cancel the task
                    cancellationTokenSource.Cancel();
                });

                try
                {
                    var longRunningTask = longRunningCancellableOperation(cancellationTokenSource.Token);

                    await longRunningTask;

                    Console.WriteLine("Press enter to continue");
                }
                catch (TaskCanceledException)
                {
                    //Console.WriteLine("Task was cancelled");
                }

                await keyBoardTask;
            }
        }

        private static void LoadSettings()
        {
            configuration = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json", false, true)
                            .Build();

            settings = new Settings();
            configuration.GetSection("app").Bind(settings);
        }
    }

    public class Options
    {
        [Option('m', "mode", Required = true, HelpText = "Run mode: publish|consume")]
        public string Mode { get; set; }
    }
}
