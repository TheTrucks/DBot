using DBot.Models.Options;
using DBot.Processing;
using DBot.Processing.Processors;
using DBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace DBot
{
    internal class Program
    {
        static async Task Main()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .Build();
            var builder = Host.CreateDefaultBuilder();
            builder.ConfigureServices(servs => ConfigureServices(servs, config));
            builder.ConfigureLogging(opts =>
                opts
                    .ClearProviders()
                    .AddConsole()
                    .AddFilter("Microsoft.Extensions.Http", LogLevel.Error)
                    .SetMinimumLevel(LogLevel.Trace));
            var host = builder.Build();

            await host.RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient("SharedHttpClient");
            services.RemoveAll<IHttpMessageHandlerBuilderFilter>(); // remove annoying http client logging
            services.Configure<AppOptions>(config.GetRequiredSection("AppOptions"));
            services.Configure<EventProcessorOptions>(config.GetRequiredSection("ProcessingOptions"));

            services.AddSingleton<RequestManager>();
            services.AddSingleton<ConnectionManager>();
            services.AddSingleton<EventProcessorManager>();
            services.AddSingleton<SystemEventsProcessor>();
            services.AddSingleton<DispatchEventsProcessor>();
            services.AddSingleton<InteractionsProcessor>();
            services.AddSingleton<GlobalCommandService>();
            services.AddSingleton<SenderService>();

            services.AddHostedService<AppService>();
        }
    }
}