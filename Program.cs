using DBot.Processing;
using DBot.Processing.Processors;
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
                    .SetMinimumLevel(LogLevel.Trace));
            var host = builder.Build();

            await host.RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            var OAI_API = config.GetValue<string>("OpenAIBaseAPI");
            if (OAI_API is null)
                throw new Exception("OpenAIBaseAPI is not configured");
            services.AddHttpClient("SharedHttpClient", opts =>
                opts.BaseAddress = new Uri(OAI_API));
            services.RemoveAll<IHttpMessageHandlerBuilderFilter>(); // remove annoying http client logging

            services.Configure<ConnectionOptions>(config.GetRequiredSection("ConnectionOptions"));
            services.Configure<EventProcessorOptions>(config.GetRequiredSection("ProcessingOptions"));
            services.AddSingleton<EventProcessorManager>();
            services.AddSingleton<SystemEventsProcessor>();

            services.AddHostedService<ConnectionManager>();
        }
    }
}