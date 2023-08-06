using DBot.Models;
using DBot.Models.EventData;
using DBot.Models.HttpModels;
using DBot.Models.HttpModels.Interaction;
using DBot.Models.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using EventCodes = DBot.Models.GatewayCode.OpCodes;

namespace DBot.Processing.Processors
{
    internal class SystemEventsProcessor
    {
        private readonly ILogger<SystemEventsProcessor> _logger;
        private readonly AppOptions _opts;
        private readonly GlobalCommandService _globalCommands;

        private delegate Task<GatewayEventBase> FunctionLink(IMemoryOwner<byte> data, int dataSize);
        private readonly Dictionary<EventCodes, FunctionLink> _codeFuncLinks = new();

        private int? _lastSeq = null;
        private Uri? _resumeGateway = null;
        private string? _sessionId = null;

        public SystemEventsProcessor(ILogger<SystemEventsProcessor> logger, IOptions<AppOptions> opts, GlobalCommandService globalCommands)
        {
            _logger = logger;
            _opts = opts.Value;
            _globalCommands = globalCommands;

            var Codes = Enum.GetValues<EventCodes>();
            foreach (var code in Codes)
            {
                switch (code)
                {
                    case EventCodes.Ready:
                        _codeFuncLinks.Add(code, ProcessReady);
                        break;
                    case EventCodes.Heartbeat:
                        _codeFuncLinks.Add(code, ProcessHeartbeat);
                        break;
                    case EventCodes.Reconnect:
                        _codeFuncLinks.Add(code, ProcessReconnect);
                        break;
                    case EventCodes.InvalidSession:
                        _codeFuncLinks.Add(code, ProcessInvalidSession);
                        break;
                    case EventCodes.Hello:
                        _codeFuncLinks.Add(code, ProcessHello);
                        break;
                    case EventCodes.HeartbeatAck:
                        _codeFuncLinks.Add(code, ProcessHeartbeatAck);
                        break;
                    default:
                        _codeFuncLinks.Add(code, NoResponse);
                        break;
                }
            }
        }

        public async Task<GatewayEventBase> ProcessSystemEvent(IMemoryOwner<byte> data, int dataSize, EventCodes code, int? seq)
        {
            if (seq is not null)
                _lastSeq = seq;

            if (_codeFuncLinks.ContainsKey(code))
                return await _codeFuncLinks[code](data, dataSize);
            else
                return await _codeFuncLinks[EventCodes.NoResponse](data, dataSize);
        }

        private Task<GatewayEventBase> NoResponse(IMemoryOwner<byte> _, int __)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayNoRespEvent());
        }

        private Task<GatewayEventBase> ProcessHeartbeatAck(IMemoryOwner<byte> _, int __)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayNoRespEvent(EventCodes.HeartbeatAck));
        }

        public Task<GatewayHeartbeatEvent> CreateHeartbeat()
        {
            return Task.FromResult(new GatewayHeartbeatEvent(_lastSeq));
        }
        private Task<GatewayEventBase> ProcessHeartbeat(IMemoryOwner<byte> data, int dataSize)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayHeartbeatEvent(_lastSeq));
        }

        private Task<GatewayEventBase> ProcessReconnect(IMemoryOwner<byte> data, int dataSize)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayNoRespEvent(EventCodes.Reconnect));
        }

        private Task<GatewayEventBase> ProcessInvalidSession(IMemoryOwner<byte> data, int dataSize)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayNoRespEvent(EventCodes.InvalidSession));
        }

        private Task<GatewayEventBase> ProcessReady(IMemoryOwner<byte> data, int dataSize)
        {
            var EventData = DeserializeData<Ready>(data, dataSize);
            if (EventData is null || EventData.Payload is null)
            {
                return NoResponse(data, dataSize);
            }

            _resumeGateway = new Uri(EventData.Payload.ResumeGateway);
            _sessionId = EventData.Payload.SessionId;

            var GlobalCmdList = _globalCommands.GetCommandsList();

            if (GlobalCmdList.Length > 0)
            {
                return Task.FromResult<GatewayEventBase>(
                    new GatewayDispatch<GlobalCommands<Interaction.AppCommandInteractionOption>>(
                        new GlobalCommands<Interaction.AppCommandInteractionOption>(GlobalCmdList)
                    ));
            }
            else
                return NoResponse(data, dataSize);
        }

        private Task<GatewayEventBase> ProcessHello(IMemoryOwner<byte> data, int dataSize)
        {
            var EventData = DeserializeData<Hello>(data, dataSize);
            if (EventData is null || EventData.Payload is null)
            {
                throw new Exception("Cannot find payload in Hello event data");
            }
            return Task.FromResult<GatewayEventBase>(
                new GatewayEvent<Hello>(
                    EventCodes.Hello,
                    EventData.Payload
                ));
        }

        public (Uri resumeGateway, string sessionId)? GetReconnectData(bool clear)
        {
            if (_resumeGateway is null || _sessionId is null)
                return null;
            else
            {
                var result = (_resumeGateway, _sessionId);
                if (clear)
                {
                    _resumeGateway = null;
                    _sessionId = null;
                }
                return result;
            }
        }

        private GatewayEvent<Payload>? DeserializeData<Payload>(IMemoryOwner<byte> data, int dataSize) where Payload : class, EventDataBase
        {
            try
            {
                var gatewayEvent = JsonSerializer.Deserialize<GatewayEvent<Payload>>(data.Memory.Span.Slice(0, dataSize));
                if (gatewayEvent is null)
                    throw new Exception("Unknown error while deserializing event data");

                return gatewayEvent;
            }
            catch (Exception ex)
            {
                _logger.LogError("Cannot deserialize gateway event data: {error}. Data size was {size}. Event type was {event}", ex.Message, dataSize, typeof(Payload).Name);
                return null;
            }
        }
    }
}
