using DBot.Models;
using DBot.Models.HttpModels.Interaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;

namespace DBot.Addons.CommandAddons.OpenAI
{
    internal sealed class OpenAIAddon
    {
        private readonly OpenAIOptions _options;
        private readonly IHttpClientFactory _httpFactory;
        public static readonly string HttpClientFactoryName = "openAIClient";

        private ConcurrentDictionary<string, List<GipityMessage>> ContextMemory = new();
        private ConcurrentDictionary<string, CancellationTokenSource> ProcessingVault = new();

        public OpenAIAddon(IOptions<OpenAIOptions> options, IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _httpFactory = httpClientFactory;
        }

        public async Task<GatewayEventBase> Invoke(InteractionCreate<AppCommandInteractionOption> interaction)
        {
            if (interaction.ChannelId is null)
                throw new ArgumentException("No channel id was provided in interaction message");

            if (ProcessingVault.ContainsKey(interaction.ChannelId))
                return SimpleMessage("Обожди, я ещё думаю, попробуй немного позже спросить", interaction.Id, interaction.Token);

            // should just fail if something's wrong, it's ok
#pragma warning disable CS8604, CS8629, CS8602
            return SimpleMessage(
                await SendRequest(
                    interaction.ChannelId,
                    GetContext(interaction.ChannelId,
                               interaction.Data.Options
                                .First(x => x.Name == "request").Value.Value
                                .GetString())),
                interaction.Id, interaction.Token);
#pragma warning restore CS8604, CS8629, CS8602
        }

        public GatewayEventBase Forget(InteractionCreate<AppCommandInteractionOption> interaction)
        {
            if (interaction.ChannelId is null)
                throw new ArgumentException("No channel id was provided in interaction message");

            Forget(interaction.ChannelId);

            return SimpleMessage("Хорошо, проехали, забыли", interaction.Id, interaction.Token);
        }

        private void Forget(string channelId)
        {
            if (ProcessingVault.TryRemove(channelId, out var request))
            {
                request.Cancel();
                request.Dispose();
            }
            ContextMemory.TryRemove(channelId, out _);
        }

        private GatewayDispatch<InteractionResponse<InteractionMessage>> SimpleMessage(string text, string interactionId, string interactionToken)
        {
            return new GatewayDispatch<InteractionResponse<InteractionMessage>>(
                new InteractionResponse<InteractionMessage>(
                    InteractionResponse<InteractionMessage>.CallbackType.CHANNEL_MESSAGE_WITH_SOURCE,
                    new InteractionMessage(
                        text,
                        null
                    ),
                    interactionId,
                    interactionToken
                )
            );
        }

        private List<GipityMessage> GetContext(string channelId, string request)
        {
            if (ContextMemory.TryGetValue(channelId, out var result))
            {
                result.Add(new GipityMessage(GipityMessage.AuthorRole.User, request));
                return result;
            }
            else
            {
                List<GipityMessage> newmessage = new();
                if (!String.IsNullOrEmpty(_options.Precontext))
                    newmessage.Add(new GipityMessage(GipityMessage.AuthorRole.System, _options.Precontext));
                ContextMemory.AddOrUpdate(channelId, newmessage, (key, val) => val = newmessage);
                newmessage.Add(new GipityMessage(GipityMessage.AuthorRole.User, request));
                return newmessage;
            }
        }

        private void UpdateContext(string channelId, string response)
        {
            ContextMemory.AddOrUpdate(
                channelId,
                GetContext(channelId, response),
                (key, val) => { 
                    val.Add(new GipityMessage(GipityMessage.AuthorRole.Assistant, response));
                    return val; 
                });
        }

        private async Task<string> SendRequest(string key, List<GipityMessage> messages)
        {
            try
            {
                var cts = new CancellationTokenSource();
                ProcessingVault.AddOrUpdate(key, cts, (key, val) => { val.Dispose(); val = cts; return val; });

                using (var SendMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions"))
                {
                    using (var _httpClient = _httpFactory.CreateClient(HttpClientFactoryName))
                    {
                        _httpClient.BaseAddress = new Uri(_options.BaseAddress);
                        _httpClient.Timeout = TimeSpan.FromSeconds(2.5 * 60);

                        SendMessage.Content = JsonContent.Create(new GipityRequest(_options.Model, messages));
                        SendMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.Token);

                        var resp = await _httpClient.SendAsync(SendMessage, cts.Token);
                        if (resp.IsSuccessStatusCode)
                        {
                            var result = await resp.Content.ReadFromJsonAsync<GipityResponse>();
                            if (result is null || result.Choices is null)
                                throw new Exception("Empty result or no choices from gipity");

                            var resultChoise = result.Choices.FirstOrDefault();
                            var resultText = resultChoise?.Message?.Content;
                            if (resultText is null)
                                throw new Exception("Empty content in response from the gipity");

                            if (resultChoise?.FinishReason is not null && resultChoise.FinishReason.ToLowerInvariant() == "length")
                            {
                                resultText += " Устал я маленько, посплю немного, разбуди как буду нужен.";
                                Forget(key);
                            }
                            else
                                UpdateContext(key, resultText);
                            return resultText;
                        }
                        else
                            throw new Exception("Empty response from the gipity");
                    }
                }
            }
            finally
            {
                if (ProcessingVault.TryRemove(key, out var cts))
                {
                    cts.Cancel();
                    cts.Dispose();
                }
            }
        }

        public static void ConfigureAddon(IServiceCollection services, IConfiguration config)
        {
            services.AddHttpClient(HttpClientFactoryName);
            var oaiConf = new ConfigurationBuilder()
                .AddJsonFile("Addons/CommandAddons/OpenAI/openAISettings.json")
                .AddEnvironmentVariables()
                .Build();
            services.Configure<OpenAIOptions>(oaiConf.GetRequiredSection("AddonSettings"));
            services.AddSingleton<OpenAIAddon>();
        }
    }
}
