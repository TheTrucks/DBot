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
using DispatchCodes = DBot.Models.GatewayCode.Dispatch;
using Microsoft.Extensions.Options;
using DBot.Models.Options;
using DBot.Models.HttpModels.Channel;

namespace DBot.Processing.Processors
{
    internal class DispatchEventsProcessor
    {
        private readonly ILogger<DispatchEventsProcessor> _logger;
        private readonly AppOptions _opts;
        private readonly InteractionsProcessor _cmdProc;

        private delegate Task<GatewayEventBase> FunctionLink(IMemoryOwner<byte> data, int dataSize);
        private readonly Dictionary<DispatchCodes, FunctionLink> _codeFuncLinks = new();

        public DispatchEventsProcessor(IOptions<AppOptions> opts, ILogger<DispatchEventsProcessor> logger, InteractionsProcessor cmdProc)
        {
            _opts = opts.Value;
            _logger = logger;
            _cmdProc = cmdProc;

            var Codes = Enum.GetValues<DispatchCodes>();
            foreach (var code in Codes)
            {
                switch (code)
                {
                    case DispatchCodes.MESSAGE_CREATE:
                        _codeFuncLinks.Add(code, ProcessPlainText);
                        break;
                    case DispatchCodes.INTERACTION_CREATE:
                        _codeFuncLinks.Add(code, ProcessInteraction);
                        break;
                    default:
                        _codeFuncLinks.Add(code, NoResponse);
                        break;
                }
            }
        }

        public async Task<GatewayEventBase> ProcessDispatchEvent(IMemoryOwner<byte> data, int dataSize, DispatchCodes code)
        {
            if (_codeFuncLinks.ContainsKey(code))
                return await _codeFuncLinks[code](data, dataSize);
            else
                return await _codeFuncLinks[DispatchCodes.UNKNOWN](data, dataSize);
        }

        private Task<GatewayEventBase> NoResponse(IMemoryOwner<byte> _, int __)
        {
            return Task.FromResult<GatewayEventBase>(new GatewayNoRespEvent());
        }

        private async Task<GatewayEventBase> ProcessPlainText(IMemoryOwner<byte> data, int dataSize)
        {
            var MessageData = DeserializeData<MessageEventCreate>(data, dataSize);
            if (MessageData is null || MessageData.Payload is null)
                return await NoResponse(data, dataSize);

            if (MessageData.Payload.Mentions.Length == 1 &&
                MessageData.Payload.Mentions.Any(x => x.Id == _opts.AppID)
                && ContentMatch(MessageData.Payload.Content, "ping"))
                return new GatewayDispatch<Message>(
                    new Message(
                        "pong", 
                        new Models.Structures.MsgRef(
                            MessageData.Payload.Id,
                            MessageData.Payload.ChannelId,
                            MessageData.Payload.GuildId)
                        )
                    );

            return await NoResponse(data, dataSize);
        }

        private async Task<GatewayEventBase> ProcessInteraction(IMemoryOwner<byte> data, int dataSize)
        {
            return await _cmdProc.ProcessInteraction(data, dataSize);
        }

        /// <summary>
        /// Ignores all the text before the mention, ignores whitespaces after the mention and returns true if rest of the content equals to the match
        /// </summary>
        private bool ContentMatch(string content, string match)
        {
            var contentSpan = content.AsSpan();
            var matchSpan = match.AsSpan();

            int indexer = 0;
            bool inBlock = false;
            bool exitedBlock = false;

            while (indexer <= contentSpan.Length - matchSpan.Length)
            {
                if (inBlock)
                {
                    if (contentSpan[indexer] == '>')
                    {
                        inBlock = false;
                        exitedBlock = true;
                    }
                }
                else
                {
                    if (exitedBlock)
                    {
                        if (!Char.IsWhiteSpace(contentSpan[indexer]))
                            return contentSpan.Slice(indexer).SequenceEqual(matchSpan);
                    }
                    else if (contentSpan[indexer] == '<')
                        inBlock = true;
                }
                indexer++;
            }

            return false;
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
                _logger.LogError("Cannot deserialize gateway dispatch event data: {error}. Data size was {size}. Event type was {event}", ex.Message, dataSize, typeof(Payload).Name);
                return null;
            }
        }
    }
}
