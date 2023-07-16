using DBot.Models;
using DBot.Models.EventData;
using Microsoft.Extensions.Logging;
using System;
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

        private delegate GatewayEventBase FunctionLink(in Memory<byte> data, in int dataSize);
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


        public GatewayEventBase ProcessInteraction(in Memory<byte> data, in int dataSize)
        {
            var interactionData = DeserializePayload<PingInteractionData>(in data, in dataSize);
            if (interactionData is null)
                return NoResponse(data, dataSize);

            return _codeFuncLinks[interactionData.InteractionType](in data, in dataSize);
        }

        private GatewayEventBase NoResponse(in Memory<byte> _, in int __)
        {
            return new GatewayNoRespEvent();
        }

        private GatewayEventBase ProcessGlobalCommand(in Memory<byte> data, in int dataSize)
        {
            var cmdData = DeserializePayload<AppCommandInteractionOption>(in data, in dataSize);
            if (cmdData is null)
            {
                _logger.LogError("Unknown deserialization global command error");
                return NoResponse(in data, in dataSize);
            }
            var globalCmd = _globalCommands.CommandInvoke(cmdData);
            if (globalCmd is null)
            {
                _logger.LogError("Could not recognize global command");
                return NoResponse(in data, in dataSize);
            }
            return globalCmd;
        }

        private InteractionCreate<Payload>? DeserializePayload<Payload>(in Memory<byte> data, in int dataSize) where Payload : class, InteractionData
        {
            try
            {
                var gatewayEvent = JsonSerializer.Deserialize<GatewayEvent<InteractionCreate<Payload>>>(data.Span.Slice(0, dataSize));
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
