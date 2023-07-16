using DBot.Models;
using DBot.Models.EventData;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Intents = DBot.Models.GatewayEventBase.Intents;
using EventCodes = DBot.Models.GatewayCode.OpCodes;
using DispatchCodes = DBot.Models.GatewayCode.Dispatch;
using DBot.Processing.Processors;
using DBot.Models.Options;

namespace DBot.Processing
{
    internal sealed class EventProcessorManager
    {
        private readonly EventProcessorOptions _options;
        private readonly SystemEventsProcessor _sysproc;
        private readonly DispatchEventsProcessor _dispproc;
        private readonly ILogger<EventProcessorManager> _logger;

        public EventProcessorManager(IOptions<EventProcessorOptions> opts, ILogger<EventProcessorManager> logger, SystemEventsProcessor sysproc, DispatchEventsProcessor dispproc)
        {
            _options = opts.Value;
            _sysproc = sysproc;
            _logger = logger;
            _dispproc = dispproc;
        }

        public (Uri resumeGateway, string sessionId)? GetReconnectData(bool clear)
        {
            return _sysproc.GetReconnectData(clear);
        }

        public GatewayEventBase ProcessEvent(in Memory<byte> data, in int dataSize)
        {
            _logger.LogDebug("Processing an event");
            var EventData = InitialProcess(in data, in dataSize);
            if (EventData is null)
                return new GatewayNoRespEvent(EventCodes.NoResponse);
            _logger.LogTrace("Processing event type is {event}", EventData.OpCode);

            if (EventData.OpCode == (int)EventCodes.Dispatch)
                return ProcessDispatch(in data, in dataSize, EventData.EventName);
            else
                return _sysproc.ProcessSystemEvent(in data, in dataSize, GatewayCode.GetOpCode(EventData.OpCode), EventData.SeqNumber);
        }

        private GatewayEventBase ProcessDispatch(in Memory<byte> data, in int dataSize, string? eventName)
        {
            if (eventName is null)
                return new GatewayNoRespEvent(EventCodes.NoResponse);

            var dispCode = GatewayCode.GetDispatch(eventName);

            if (dispCode == DispatchCodes.READY)
                return _sysproc.ProcessSystemEvent(in data, in dataSize, EventCodes.Ready, null);

            return _dispproc.ProcessDispatchEvent(in data, in dataSize, dispCode);
        }

        public GatewayHeartbeatEvent CreateHeartbeat()
        {
            return _sysproc.CreateHeartbeat();
        }

        public GatewayEventBase CreateIdentity()
        {
            return new GatewayEvent<Identity>
            (
                EventCodes.Identity,
                new Identity
                (
                    _options.Token,
                    (int)(Intents.GuildMessages | Intents.MessageContent),
                    "linux",
                    "DBot",
                    "DBot"
                 )
            );
        }

        private GatewayNoRespEvent? InitialProcess(in Memory<byte> data, in int dataSize)
        {
            try
            {
                var dataSelect = data.Span.Slice(0, dataSize);
                var gatewayEvent = JsonSerializer.Deserialize<GatewayNoRespEvent>(dataSelect);
                if (gatewayEvent is null)
                    throw new Exception("Unknown error while retrieving gateway event code");
                _logger.LogTrace("Event preview: {text_prev}", Encoding.UTF8.GetString(dataSelect));
                return gatewayEvent;

            }
            catch (Exception ex)
            {
                _logger.LogError("Cannot deserialize gateway event data: {error}. Data size was {size}", ex.Message, dataSize);
                return null;
            }
        }
    }
}
