using DBot.Models;
using DBot.Models.EventData;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;

namespace DBot.Processing.Processors
{
    internal sealed class InteractionsProcessor
    {
        private readonly ILogger<InteractionsProcessor> _logger;
        private readonly GlobalCommandService _globalCommands;

        private delegate Task<GatewayEventBase> FunctionLink(IMemoryOwner<byte> data, int dataSize);
        private readonly Dictionary<InteractionType, FunctionLink> _codeFuncLinks = new();

        public InteractionsProcessor(ILogger<InteractionsProcessor> logger, GlobalCommandService globalCommands)
        {
            _logger = logger;
            _globalCommands = globalCommands;

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

            return await _globalCommands.CommandInvoke(cmdData);
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
