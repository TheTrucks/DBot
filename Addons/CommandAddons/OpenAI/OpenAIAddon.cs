using DBot.Addons.CommandAddons.HttpCat;
using DBot.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;

namespace DBot.Addons.CommandAddons.OpenAI
{
    internal sealed class OpenAIAddon
    {
        private readonly OpenAIOptions _options;
        private readonly ILogger<OpenAIAddon> _logger;
        private readonly IHttpClientFactory _httpFactory;
        public static readonly string HttpClientFactoryName = "openAIClient";

        public OpenAIAddon(IOptions<OpenAIOptions> options, ILogger<OpenAIAddon> logger, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _logger = logger;
            _httpFactory = httpClientFactory;
        }

        public async Task<GatewayEventBase> Invoke(InteractionCreate<AppCommandInteractionOption> interaction)
        {

        }

        public static void ConfigureAddon(IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient(HttpClientFactoryName);
            var oaiConf = new ConfigurationBuilder()
                .AddJsonFile("Addons/CommandAddons/OpenAI/openAISettings.json")
                .AddEnvironmentVariables()
                .Build();
            services.Configure<HttpCatOptions>(oaiConf.GetRequiredSection("AddonSettings"));
            services.AddSingleton<OpenAIAddon>();
        }
    }
}
