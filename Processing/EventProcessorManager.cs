﻿using DBot.Models;
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

namespace DBot.Processing
{
    internal sealed class EventProcessorOptions
    {
        public required string Token { get; set; }
    }
    internal sealed class EventProcessorManager
    {
        private readonly EventProcessorOptions _options;
        private readonly SystemEventsProcessor _sysproc;
        private readonly ILogger<EventProcessorManager> _logger;

        public EventProcessorManager(IOptions<EventProcessorOptions> opts, SystemEventsProcessor sysproc, ILogger<EventProcessorManager> logger)
        {
            _options = opts.Value;
            _sysproc = sysproc;
            _logger = logger;
        }

        public (Uri resumeGateway, string sessionId)? GetReconnectData(bool clear)
        {
            return _sysproc.GetReconnectData(clear);
        }

        public GatewayEventBase ProcessEvent(IMemoryOwner<byte> data, in int dataSize)
        {
            _logger.LogDebug("Processing an event");
            var EventData = InitialProcess(data, dataSize);
            if (EventData is null)
                return new GatewayNoRespEvent(EventCodes.NoResponse);
            _logger.LogTrace("Processing event type is {event}", EventData.OpCode);

            if (EventData.OpCode == (int)EventCodes.Dispatch)
                return ProcessDispatch(data, dataSize, EventData.EventName);
            else
                return _sysproc.ProcessSystemEvent(data, dataSize, GatewayCode.GetOpCode(EventData.OpCode), EventData.SeqNumber);
            
        }

        private GatewayEventBase ProcessDispatch(IMemoryOwner<byte> data, in int dataSize, string? eventName)
        {
            if (eventName is null)
                return new GatewayNoRespEvent(EventCodes.NoResponse);

            if (GatewayCode.GetDispatch(eventName) == DispatchCodes.READY)
                return _sysproc.ProcessSystemEvent(data, dataSize, EventCodes.Ready, null);

            return new GatewayNoRespEvent(EventCodes.NoResponse);
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

        private GatewayNoRespEvent? InitialProcess(IMemoryOwner<byte> data, in int dataSize)
        {
            try
            {
                var dataSelect = data.Memory.Span.Slice(0, dataSize);
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
