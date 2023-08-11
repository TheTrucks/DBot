using DBot.Models;
using DBot.Models.HttpModels;
using DBot.Models.HttpModels.Channel;
using DBot.Models.HttpModels.Interaction;
using DBot.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;
using static DBot.Models.HttpModels.Interaction.InteractionMessage;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DBot.Processing
{
    internal sealed class GlobalCommandService
    {
        private readonly ILogger<GlobalCommandService> _logger;
        private readonly AppOptions _opts;
        private readonly IHttpClientFactory _httpFactory;

        private readonly GlobalCommand<AppCommandInteractionOption>[] CommandList;

        private delegate Task<GatewayEventBase> FunctionLink(InteractionCreate<AppCommandInteractionOption> command);
        private readonly Dictionary<string, FunctionLink> _nameFuncLinks = new();

        private readonly static string[] _errAnswers =
        {
            "А? Не понял концепции чё-то.",
            "Не услышал, можешь повторить через донат?",
            "Что-то чудное совсем мелешь, не понимаю."
        };

        public GlobalCommandService(ILogger<GlobalCommandService> logger, IOptions<AppOptions> opts, IHttpClientFactory httpFactory)
        {
            _logger = logger;
            _opts = opts.Value;
            _httpFactory = httpFactory;

            CommandList = new GlobalCommand<AppCommandInteractionOption>[]
            {
                new GlobalCommand<AppCommandInteractionOption>(
                    "ping",
                    "сасёт ногу",
                    new AppCommandInteractionOption[]{
                        new AppCommandInteractionOption(
                            "public",
                            AppCommandInteractionOption.AppCommandOptionType.BOOLEAN,
                            "Allow others to see bot's answer in the chat (default: true)",
                            null,
                            false,
                            null
                            )
                        }),
                new GlobalCommand<AppCommandInteractionOption>(
                    "http-cat",
                    "Returns picture of a cat by http code",
                    new AppCommandInteractionOption[]
                    {
                        new AppCommandInteractionOption(
                            "code",
                            AppCommandInteractionOption.AppCommandOptionType.INTEGER,
                            "Http code of the desired cat",
                            null,
                            true,
                            null
                        ),
                        new AppCommandInteractionOption(
                            "public",
                            AppCommandInteractionOption.AppCommandOptionType.BOOLEAN,
                            "Allow others to see bot's answer in the chat (default: true)",
                            null,
                            false,
                            null
                        )
                    }),
                new GlobalCommand<AppCommandInteractionOption>(
                    "error",
                    "вызывает ошибку в консоли",
                    null
                    )
            };

            _nameFuncLinks.Add("ping", PingInvoke);
            _nameFuncLinks.Add("http-cat", HttpCatInvoke);
        }

        public GlobalCommand<AppCommandInteractionOption>[] GetCommandsList()
        {
            return CommandList;
        }

        public async Task<GatewayEventBase> CommandInvoke(InteractionCreate<AppCommandInteractionOption> command)
        {
            if (command.Data is null)
            {
                _logger.LogError("No command data found in message {msg_id}", command.Id);
                return ErrorResponse(command, "Полная белиберда, ничего не ясно");
            }

            if (_nameFuncLinks.TryGetValue(command.Data.Name, out var function))
                return await function(command);

            _logger.LogError("Could not recognize global command");
            var errAns = Random.Shared.Next(0, _errAnswers.Length);
            return ErrorResponse(command, $"{_errAnswers[errAns]} Таких слов, как {command.Data.Name} в жизни не слыхал");
        }

        private GatewayEventBase ErrorResponse(InteractionCreate<AppCommandInteractionOption> command, string message)
        {
            return new GatewayDispatch<InteractionResponse<InteractionMessage>>(
                    new InteractionResponse<InteractionMessage>(
                        InteractionResponse<InteractionMessage>.CallbackType.CHANNEL_MESSAGE_WITH_SOURCE,
                        new InteractionMessage(
                            message,
                            InteractionFlags.EPHEMERAL
                        ),
                        command.Id,
                        command.Token
                    )
            );
        }

        private Task<GatewayEventBase> PingInvoke(InteractionCreate<AppCommandInteractionOption> command)
        {
            InteractionFlags? ephemeralFlag = null;
            try
            {
                var findOption = command.Data?.Options?.FirstOrDefault(x => x.Name == "public")?.Value?.GetBoolean();
                if ((findOption.HasValue && findOption.Value == false) || !findOption.HasValue)
                    ephemeralFlag = InteractionFlags.EPHEMERAL;
            }
            catch { }

            return Task.FromResult<GatewayEventBase>(new GatewayDispatch<InteractionResponse<InteractionMessage>>(
                    new InteractionResponse<InteractionMessage>(
                        InteractionResponse<InteractionMessage>.CallbackType.CHANNEL_MESSAGE_WITH_SOURCE,
                        new InteractionMessage(
                            "pong",
                            ephemeralFlag
                        ),
                        command.Id,
                        command.Token
                    )
            ));
        }

        private async Task<GatewayEventBase> HttpCatInvoke(InteractionCreate<AppCommandInteractionOption> command)
        {
            InteractionFlags? ephemeralFlag = null;
            int catCode = 404;
            if (command.Data?.Options?.Length > 0)
            {
                try
                {
                    foreach (var option in command.Data.Options)
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
                using (var SendMessage = new HttpRequestMessage(
                HttpMethod.Get,
                catCode.ToString()))
                {
                    using (var _httpClient = _httpFactory.CreateClient("SharedHttpClient"))
                    {
                        _httpClient.BaseAddress = new Uri(_opts.HttpCatsBaseURL);

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
                                $"{_opts.HttpCatsBaseURL}{cat}.jpg",
                                ephemeralFlag
                            ),
                            command.Id,
                            command.Token
                        )
                );
            }
            else
            {
                _logger.LogError("HttpCat command data is not present in request");
                return ErrorResponse(command, "Кажется, как-то неправильно я просьбу расслышал, очень невнятно всё");
            }
        }
    }
}
