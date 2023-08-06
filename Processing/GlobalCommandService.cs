using DBot.Models;
using DBot.Models.HttpModels;
using DBot.Models.HttpModels.Channel;
using DBot.Models.HttpModels.Interaction;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;
using static DBot.Models.HttpModels.Interaction.InteractionMessage;

namespace DBot.Processing
{
    internal sealed class GlobalCommandService
    {
        private readonly ILogger<GlobalCommandService> _logger;

        private readonly GlobalCommand<AppCommandInteractionOption>[] CommandList;

        private delegate GatewayEventBase FunctionLink(InteractionCreate<AppCommandInteractionOption> command);
        private readonly Dictionary<string, FunctionLink> _nameFuncLinks = new();

        private readonly static string[] _appeals =
        {
            "buddy",
            "pal",
            "dude",
            "amigo",
            "comrade",
            "bro",
            "mate",
            "friend"
        };

        public GlobalCommandService(ILogger<GlobalCommandService> logger)
        {
            _logger = logger;

            CommandList = new GlobalCommand<AppCommandInteractionOption>[]
            {
                new GlobalCommand<AppCommandInteractionOption>(
                    "ping",
                    "сасёт ногу",
                    new AppCommandInteractionOption[]{
                        new AppCommandInteractionOption(
                            "public",
                            AppCommandInteractionOption.AppCommandOptionType.BOOLEAN,
                            "Allow others to see bot's answer in the chat",
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
        }

        public GlobalCommand<AppCommandInteractionOption>[] GetCommandsList()
        {
            return CommandList;
        }

        public GatewayEventBase CommandInvoke(InteractionCreate<AppCommandInteractionOption> command)
        {
            if (command.Data is null)
            {
                _logger.LogError("No command data found in message {msg_id}", command.Id);
                return ErrorResponse(command, "Looks like the command name you sent wasn't correctly received");
            }

            if (_nameFuncLinks.TryGetValue(command.Data.Name, out var function))
                return function(command);

            _logger.LogError("Could not recognize global command");
            var appeal = Random.Shared.Next(0, _appeals.Length);
            return ErrorResponse(command, $"Sorry {_appeals[appeal]}, but the command named {command.Data.Name} isn't registered on the backend");
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

        private GatewayEventBase PingInvoke(InteractionCreate<AppCommandInteractionOption> command)
        {
            InteractionFlags? ephemeralFlag = null;
            try
            {
                var findOption = command.Data?.Options?.FirstOrDefault(x => x.Name == "public")?.Value?.GetBoolean();
                if ((findOption.HasValue && findOption.Value == false) || !findOption.HasValue)
                    ephemeralFlag = InteractionFlags.EPHEMERAL;
            }
            catch { }

            return new GatewayDispatch<InteractionResponse<InteractionMessage>>(
                    new InteractionResponse<InteractionMessage>(
                        InteractionResponse<InteractionMessage>.CallbackType.CHANNEL_MESSAGE_WITH_SOURCE,
                        new InteractionMessage(
                            "pong",
                            ephemeralFlag
                        ),
                        command.Id,
                        command.Token
                    )
            );
        }
    }
}
