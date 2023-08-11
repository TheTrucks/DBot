using DBot.Models;
using DBot.Models.EventData;
using DBot.Models.HttpModels;
using DBot.Models.HttpModels.Channel;
using DBot.Models.HttpModels.Interaction;
using DBot.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DBot.Services
{
    internal sealed class SenderService
    {
        private readonly ILogger<SenderService> _logger;
        private readonly IHttpClientFactory _httpFactory;
        private readonly AppOptions _opts;
        private readonly string BotToken;

        private delegate Task FunctionLink(GatewayEventBase DataWrapped, CancellationToken _token);
        private readonly Dictionary<Type, FunctionLink> _codeFuncLinks = new(); 

        public SenderService(IOptions<AppOptions> appOpts, IOptions<EventProcessorOptions> procOpts, ILogger<SenderService> logger, IHttpClientFactory httpFactory)
        {
            _opts = appOpts.Value;
            BotToken = procOpts.Value.Token;
            _logger = logger;
            _httpFactory = httpFactory;

            _codeFuncLinks.Add(typeof(GatewayDispatch<Message>), SendReplyMessage);
            _codeFuncLinks.Add(typeof(GatewayDispatch<InteractionResponse<InteractionMessage>>), SendInteractionResp);
            _codeFuncLinks.Add(typeof(GatewayDispatch<ReadyInfo<Interaction.AppCommandInteractionOption>>), UpdateGlobalCommands);
            _codeFuncLinks.Add(typeof(GatewayDispatch<PlainCommand>), SendPlainMessage);
        }

        public async Task SendAnswer(WebSocket CWS, GatewayEventBase Answer, CancellationToken _token)
        {
            _logger.LogDebug("Sending answer of type {type}", GatewayCode.GetOpCode(Answer.OpCode).ToString());
            using (var DataMS = new MemoryStream())
            {
                JsonSerializer.Serialize(DataMS, Answer, Answer.GetType());
                using (var DataMemory = MemoryPool<byte>.Shared.Rent((int)DataMS.Length))
                {
                    DataMS.Seek(0, SeekOrigin.Begin);
                    int read = await DataMS.ReadAsync(DataMemory.Memory, _token);
                    await CWS.SendAsync(DataMemory.Memory.Slice(0, read), WebSocketMessageType.Binary, true, _token);
                }
            }
            _logger.LogDebug("Answer sent");
        }

        public async Task SendWebhook(GatewayEventBase DataWrapped, CancellationToken _token)
        {
            var dataType = DataWrapped.GetType();
            _logger.LogDebug("Sending webhook of type {type}", dataType.FullName);
            
            if (!_codeFuncLinks.ContainsKey(dataType))
            {
                _logger.LogDebug("No corresponding webhook type found");
                return;
            }

            await _codeFuncLinks[dataType](DataWrapped, _token);
        }

        private async Task SendInteractionResp(GatewayEventBase DataWrapped, CancellationToken _token)
        {
            var Data = ((GatewayDispatch<InteractionResponse<InteractionMessage>>)DataWrapped).Payload;
            using (var SendMessage = new HttpRequestMessage(
                Data.Method,
                Data.GetUri()))
            {
                using (var _httpClient = _httpFactory.CreateClient("SharedHttpClient"))
                {
                    _httpClient.BaseAddress = new Uri(_opts.BaseURL);

                    SendMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", BotToken);
                    SendMessage.Content = JsonContent.Create(Data);

                    var resp = await _httpClient.SendAsync(SendMessage);
                    if (!resp.IsSuccessStatusCode)
                    {
                        LogHttpError(resp);
                    }
                }
            }
        }

        private async Task SendReplyMessage(GatewayEventBase DataWrapped, CancellationToken _token)
        {
            var Data = ((GatewayDispatch<Message>)DataWrapped).Payload;
            using (var SendMessage = new HttpRequestMessage(
                Data.Method,
                Data.GetUri(Data.MessageReference!.ChannelId!)))
            {
                using (var _httpClient = _httpFactory.CreateClient("SharedHttpClient"))
                {
                    _httpClient.BaseAddress = new Uri(_opts.BaseURL);

                    SendMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", BotToken);
                    SendMessage.Content = JsonContent.Create(Data);

                    var resp = await _httpClient.SendAsync(SendMessage);
                    if (!resp.IsSuccessStatusCode)
                    {
                        LogHttpError(resp);
                    }
                }
            }
        }

        private async Task UpdateGlobalCommands(GatewayEventBase DataWrapped, CancellationToken _token)
        {
            var Data = DataWrapped as GatewayDispatch<ReadyInfo<Interaction.AppCommandInteractionOption>>;
            if (Data is null)
                Data = new GatewayDispatch<ReadyInfo<Interaction.AppCommandInteractionOption>>(new ReadyInfo<Interaction.AppCommandInteractionOption>(Array.Empty<GlobalCommand<Interaction.AppCommandInteractionOption>>()));

            var currentCommands = await RetrieveCurrentCommands(_token);

            if (!Interaction.AppCommandInteractionOption.Comparer.Equals(Data.Payload.Payload, currentCommands))
            {
                using (var SendMessage = new HttpRequestMessage(
                    Data.Payload.Method,
                    Data.Payload.GetUri(_opts.AppID)))
                {
                    using (var _httpClient = _httpFactory.CreateClient("SharedHttpClient"))
                    {
                        _httpClient.BaseAddress = new Uri(_opts.BaseURL);

                        SendMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", BotToken);
                        SendMessage.Content = JsonContent.Create(Data.Payload.Payload);

                        var resp = await _httpClient.SendAsync(SendMessage);
                        if (!resp.IsSuccessStatusCode)
                        {
                            LogHttpError(resp);
                        }
                    }
                }
            }
        }

        private async Task<GlobalCommand<Interaction.AppCommandInteractionOption>[]> RetrieveCurrentCommands(CancellationToken _token)
        {
            using (var SendMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"applications/{_opts.AppID}/commands"))
            {
                using (var _httpClient = _httpFactory.CreateClient("SharedHttpClient"))
                {
                    _httpClient.BaseAddress = new Uri(_opts.BaseURL);

                    SendMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", BotToken);

                    var resp = await _httpClient.SendAsync(SendMessage);
                    if (!resp.IsSuccessStatusCode)
                    {
                        LogHttpError(resp);
                        return Array.Empty<GlobalCommand<Interaction.AppCommandInteractionOption>>();
                    }

                    return await DeserializeCurrentCommands(resp);
                }
            }
        }

        private async Task<GlobalCommand<Interaction.AppCommandInteractionOption>[]> DeserializeCurrentCommands(HttpResponseMessage resp)
        {
            try
            {
                using var dataStream = await resp.Content.ReadAsStreamAsync();
                var cmds = await JsonSerializer.DeserializeAsync<GlobalCommand<Interaction.AppCommandInteractionOption>[]>(dataStream);
                if (cmds is null)
                    return Array.Empty<GlobalCommand<Interaction.AppCommandInteractionOption>>();
                return cmds;
            }
            catch (Exception ex)
            {
                _logger.LogError("Cannot deserialize current global commands: {msg}", ex.Message);
                return Array.Empty<GlobalCommand<Interaction.AppCommandInteractionOption>>();
            }
        }

        private void LogHttpError(HttpResponseMessage message)
        {
            using var dataStream = message.Content.ReadAsStream();
            Span<byte> dataBuffer = stackalloc byte[(int)dataStream.Length];
            int read = dataStream.Read(dataBuffer);
            string data = Encoding.UTF8.GetString(dataBuffer.Slice(0, read));
            _logger.LogError("Reply message send error: {err}", data);
        }

        private async Task SendPlainMessage(GatewayEventBase DataWrapped, CancellationToken _token)
        {
            var Data = ((GatewayDispatch<PlainCommand>)DataWrapped).Payload;
            using (var SendMessage = new HttpRequestMessage(
                Data.Method,
                Data.GetUri()))
            {
                using (var _httpClient = _httpFactory.CreateClient("SharedHttpClient"))
                {
                    _httpClient.BaseAddress = new Uri(_opts.BaseURL);

                    SendMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", BotToken);
                    SendMessage.Content = new StringContent(Data.Payload, Encoding.UTF8, "application/json");

                    var resp = await _httpClient.SendAsync(SendMessage);
                    if (!resp.IsSuccessStatusCode)
                    {
                        LogHttpError(resp);
                    }
                }
            }
        }
    }
}
