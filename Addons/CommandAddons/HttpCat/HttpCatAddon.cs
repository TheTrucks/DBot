using DBot.Models;
using DBot.Models.HttpModels.Interaction;
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
using static DBot.Models.HttpModels.Interaction.InteractionMessage;

namespace DBot.Addons.CommandAddons.HttpCat
{
    internal sealed class HttpCatAddon
    {
        static readonly string HttpClientFactoryName = "httpCatClient";
        private readonly HttpCatOptions _options;
        private readonly ILogger<HttpCatAddon> _logger;
        private readonly IHttpClientFactory _httpFactory;
        public HttpCatAddon(IOptions<HttpCatOptions> opts, ILogger<HttpCatAddon> logger, IHttpClientFactory httpClientFactory)
        {
            _options = opts.Value;
            _logger = logger;
            _httpFactory = httpClientFactory;
        }

        public async Task<GatewayEventBase> Invoke(InteractionCreate<AppCommandInteractionOption> interaction)
        {
            InteractionFlags? ephemeralFlag = null;
            int catCode = 404;
            if (interaction.Data?.Options?.Length > 0)
            {
                try
                {
                    foreach (var option in interaction.Data.Options)
                    {
                        switch (option.Name)
                        {
                            case "public":
                                if (option.Value.HasValue)
                                    if (!option.Value.Value.GetBoolean())
                                        ephemeralFlag = InteractionFlags.EPHEMERAL;
                                break;
                            case "code":
                                if (option.Value.HasValue)
                                    option.Value.Value.TryGetInt32(out catCode);
                                break;
                        }
                    }
                }
                catch { }

                string cat = "404";
                using (var SendMessage = new HttpRequestMessage(HttpMethod.Get, catCode.ToString()))
                {
                    using (var _httpClient = _httpFactory.CreateClient(HttpClientFactoryName))
                    {
                        _httpClient.BaseAddress = new Uri(_options.BaseAddress);

                        var resp = await _httpClient.SendAsync(SendMessage);
                        if (resp.IsSuccessStatusCode)
                        {
                            cat = catCode.ToString();
                        }
                    }
                }

                return new GatewayDispatch<InteractionResponse<InteractionMessage>>(
                        new InteractionResponse<InteractionMessage>(
                            InteractionResponse<InteractionMessage>.CallbackType.CHANNEL_MESSAGE_WITH_SOURCE,
                            new InteractionMessage(
                                $"{_options.BaseAddress}{cat}{_options.Suffix}",
                                ephemeralFlag
                            ),
                            interaction.Id,
                            interaction.Token
                        )
                );
            }
            else
            {
                _logger.LogError("HttpCat command data is not present in request");
                throw new Exception("Кажется, как-то неправильно я просьбу расслышал, очень невнятно всё");
            }
        }

        public static void ConfigureAddon(IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient(HttpClientFactoryName);
            var catConf = new ConfigurationBuilder()
                .AddJsonFile("Addons/CommandAddons/HttpCat/httpCatSetting.json")
                .AddEnvironmentVariables()
                .Build();
            services.Configure<HttpCatOptions>(catConf.GetRequiredSection("AddonSettings"));
            services.AddSingleton<HttpCatAddon>();
        }
    }
}
