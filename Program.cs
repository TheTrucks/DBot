using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            var host = builder.Build();

            await host.RunAsync();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration config)
        {
            services.Configure<ConnectionOptions>(config.GetSection("ConnectionOptions"));
            services.AddSingleton<EventProcessor>();

            services.AddHostedService<ConnectionManager>();
        }
    }
}