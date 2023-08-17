using DBot.Models;
using DBot.Models.EventData;
using DBot.Models.HttpModels.Channel;
using DBot.Models.HttpModels.Interaction;
using DBot.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using static DBot.Models.EventData.Interaction;
using static DBot.Models.HttpModels.Interaction.InteractionMessage;

namespace DBot.Processing.Processors
{
    internal sealed class InteractionsProcessor
    {
        private readonly ILogger<InteractionsProcessor> _logger;
        private readonly GlobalCommandService _globalCommands;
        private readonly SenderService _senderService;

        private delegate Task<GatewayEventBase> FunctionLink(IMemoryOwner<byte> data, int dataSize);
        private readonly Dictionary<InteractionType, FunctionLink> _codeFuncLinks = new();

        public InteractionsProcessor(ILogger<InteractionsProcessor> logger, GlobalCommandService globalCommands, SenderService senderService)
        {
            _logger = logger;
            _globalCommands = globalCommands;
            _senderService = senderService;

            var Codes = Enum.GetValues<InteractionType>();
            foreach (var code in Codes)
            {
                switch (code)
                {
                    case InteractionType.PING:
                        _codeFuncLinks.Add(code, NoResponse);
                        break;
                    case InteractionType.APPLICATION_COMMAND:
                        _codeFuncLinks.Add(code, ProcessGlobalCommand);
                        break;
                    default:
                        _codeFuncLinks.Add(code, NoResponse);
                        break;
                }
            }
            _globalCommands = globalCommands;
        }


        public async Task<GatewayEventBase> ProcessInteraction(IMemoryOwner<byte> data, int dataSize)
        {
            var interactionData = DeserializePayload<PingInteractionData>(data, dataSize);
            if (interactionData is null)
                return await NoResponse(data, dataSize);

            return await _codeFuncLinks[interactionData.InteractionType](data, dataSize);
        }

        private Task<GatewayEventBase> NoResponse(IMemoryOwner<byte> _, int __)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayNoRespEvent());
        }

        private async Task<GatewayEventBase> ProcessGlobalCommand(IMemoryOwner<byte> data, int dataSize)
        {
            var cmdData = DeserializePayload<AppCommandInteractionOption>(data, dataSize);
            if (cmdData is null)
            {
                _logger.LogError("Unknown deserialization global command error");
                return await NoResponse(data, dataSize);
            }

            bool timedOut = false;
            var timeoutTimer = new Timer(async (object? state) =>
            {
                timedOut = true;
                await CreateAwaitedResponse(state);
            }, Tuple.Create(cmdData.Id, cmdData.Token), TimeSpan.FromMilliseconds(2200), Timeout.InfiniteTimeSpan);
            var result = await _globalCommands.CommandInvoke(cmdData);
            await timeoutTimer.DisposeAsync();
            if (timedOut)
            {
                var payload = ((GatewayDispatch<InteractionResponse<InteractionMessage>>)result).Payload;
                payload.SetUri(HttpMethod.Post, $"webhooks/{cmdData.AppId}/{cmdData.Token}", 0, false);
            }
            return result;
        }

        private GatewayDispatch<InteractionResponse<InteractionMessage>> InteractionWaitMessage(string interactionId, string interactionToken)
        {
            return new GatewayDispatch<InteractionResponse<InteractionMessage>>(
                    new InteractionResponse<InteractionMessage>(
                        InteractionResponse<InteractionMessage>.CallbackType.CHANNEL_MESSAGE_WITH_SOURCE,
                        new InteractionMessage(
                            "Подожди секундочку, дедушке надо подумать",
                            null
                        ),
                        interactionId,
                        interactionToken
                    )
            );
        }

        private async Task CreateAwaitedResponse(object? input)
        {
            if (input is null)
                return;
            var inputData = (Tuple<string, string>)input;

            await _senderService.SendWebhook(InteractionWaitMessage(inputData.Item1, inputData.Item2), CancellationToken.None);
        }

        private InteractionCreate<Payload>? DeserializePayload<Payload>(IMemoryOwner<byte> data, int dataSize) where Payload : class, InteractionData
        {
            try
            {
                var gatewayEvent = JsonSerializer.Deserialize<GatewayEvent<InteractionCreate<Payload>>>(data.Memory.Span.Slice(0, dataSize));
                if (gatewayEvent?.Payload is null)
                    throw new Exception("Unknown error while deserializing interaction data");

                return gatewayEvent.Payload;
            }
            catch (Exception ex)
            {
                _logger.LogError("Cannot deserialize gateway interaction event data: {error}. Data size was {size}. Event type was {event}", ex.Message, dataSize, typeof(Payload).Name);
                return null;
            }
        }
    }
}
