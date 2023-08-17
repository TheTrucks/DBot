using DBot.Addons.CommandAddons.HttpCat;
using DBot.Addons.CommandAddons.OpenAI;
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

namespace DBot.Processing
{
    internal sealed class GlobalCommandService
    {
        private readonly ILogger<GlobalCommandService> _logger;
        private readonly HttpCatAddon _catAddon;
        private readonly OpenAIAddon _gipityAddon;

        private readonly GlobalCommand<AppCommandInteractionOption>[] CommandList;

        private delegate Task<GatewayEventBase> FunctionLink(InteractionCreate<AppCommandInteractionOption> command);
        private readonly Dictionary<string, FunctionLink> _nameFuncLinks = new();

        private readonly static string[] _errAnswers =
        {
            "А? Не понял концепции чё-то.",
            "Не услышал, можешь повторить через донат?",
            "Что-то чудное совсем мелешь, не понимаю."
        };

        public GlobalCommandService(ILogger<GlobalCommandService> logger, HttpCatAddon catAddon, OpenAIAddon gipityAddon)
        {
            _logger = logger;
            _catAddon = catAddon;
            _gipityAddon = gipityAddon;

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
                    ),
                new GlobalCommand<AppCommandInteractionOption>(
                    "gipity",
                    "Шлёт запрос в джИпити",
                    new AppCommandInteractionOption[]
                    {
                        new AppCommandInteractionOption(
                            "request",
                            AppCommandInteractionOption.AppCommandOptionType.STRING,
                            "Непосредственно запрос к джИпити",
                            null,
                            true,
                            null
                        )
                    }
                ),
                new GlobalCommand<AppCommandInteractionOption>(
                    "gipity-forget",
                    "Очищает контекст диалога и завершает ожидаемые ответы",
                    null
                )
            };

            _nameFuncLinks.Add("ping", PingInvoke);
            _nameFuncLinks.Add("http-cat", HttpCatInvoke);
            _nameFuncLinks.Add("gipity", GipityInvoke);
            _nameFuncLinks.Add("gipity-forget", GipityForget);
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
            try
            {
                return await _catAddon.Invoke(command);
            }
            catch (Exception ex)
            {
                return ErrorResponse(command, ex.Message);
            }
        }

        private async Task<GatewayEventBase> GipityInvoke(InteractionCreate<AppCommandInteractionOption> command)
        {
            try
            {
                return await _gipityAddon.Invoke(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error trying to make a gipity request");
                return ErrorResponse(command, "У джИпити произошла ЧУДОВИЩНАЯ ошибка");
            }
        }

        private Task<GatewayEventBase> GipityForget(InteractionCreate<AppCommandInteractionOption> command)
        {
            try
            {
                return Task.FromResult(_gipityAddon.Forget(command));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "There was an error trying to forget a gipity request");
                return Task.FromResult(ErrorResponse(command, "У джИпити произошла ЧУДОВИЩНАЯ ошибка"));
            }
        }
    }
}
