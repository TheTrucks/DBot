using DBot.Models;
using DBot.Models.EventData;
using DBot.Models.HttpModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models
{
    public static class GatewayCode
    {
        private readonly static Dictionary<int, OpCodes> codesList = new();
        private readonly static Dictionary<string, Dispatch> dispatchList = new();

        static GatewayCode()
        {
            var CodeVals = Enum.GetValues<OpCodes>();
            var DispVals = Enum.GetValues<Dispatch>();

            for (int i = 0; i < CodeVals.Length; i++)
            {
                codesList.Add((int)CodeVals[i], CodeVals[i]);
            }
            for (int i = 0; i < DispVals.Length; i++)
            {
                dispatchList.Add(DispVals[i].ToString(), DispVals[i]);
            }
        }

        public static OpCodes GetOpCode(int code)
        {
            if (codesList.TryGetValue(code, out OpCodes result))
            {
                return result;
            }
            return OpCodes.NoResponse;
        }

        public static Dispatch GetDispatch(string name)
        {
            if (dispatchList.TryGetValue(name.ToUpperInvariant(), out Dispatch result))
            {
                return result;
            }
            return Dispatch.UNKNOWN;
        }

        public enum OpCodes : int
        {
            Ready = -10,
            NoResponse = -1,
            Dispatch = 0,
            Heartbeat = 1,
            Identity = 2,
            Resume = 6,
            Reconnect = 7,
            InvalidSession = 9,
            Hello = 10,
            HeartbeatAck = 11
        }

        public enum Dispatch : int
        {
            UNKNOWN = -1,
            READY = 0,
            MESSAGE_CREATE = 1,
            INTERACTION_CREATE = 2
        }
    }

    public abstract class GatewayEventBase
    {
        public GatewayEventBase(int opCode, int? seqNumber, string? eventName) 
        {
            OpCode = opCode;
            SeqNumber = seqNumber;
            EventName = eventName;
        }

        [JsonPropertyName("op")]
        public int OpCode { get; set; }

        [JsonPropertyName("s")]
        public int? SeqNumber { get; set; }

        [JsonPropertyName("t")]
        public string? EventName { get; set; }

        [Flags]
        public enum Intents
        {
            Guilds = 1 << 0,
            /*
                  - GUILD_CREATE
                  - GUILD_UPDATE
                  - GUILD_DELETE
                  - GUILD_ROLE_CREATE
                  - GUILD_ROLE_UPDATE
                  - GUILD_ROLE_DELETE
                  - CHANNEL_CREATE
                  - CHANNEL_UPDATE
                  - CHANNEL_DELETE
                  - CHANNEL_PINS_UPDATE
                  - THREAD_CREATE
                  - THREAD_UPDATE
                  - THREAD_DELETE
                  - THREAD_LIST_SYNC
                  - THREAD_MEMBER_UPDATE
                  - THREAD_MEMBERS_UPDATE
                  - STAGE_INSTANCE_CREATE
                  - STAGE_INSTANCE_UPDATE
                  - STAGE_INSTANCE_DELETE
            */
            GuildMembers = 1 << 1,
            /*
                  - GUILD_MEMBER_ADD
                  - GUILD_MEMBER_UPDATE
                  - GUILD_MEMBER_REMOVE
                  - THREAD_MEMBERS_UPDATE
             */
            GuildMod = 1 << 2,
            /*
                  - GUILD_AUDIT_LOG_ENTRY_CREATE
                  - GUILD_BAN_ADD
                  - GUILD_BAN_REMOVE
             */
            GuildEmojisStickers = 1 << 3,
            /*
                  - GUILD_EMOJIS_UPDATE
                  - GUILD_STICKERS_UPDATE
             */
            GuildIntegrations = 1 << 4,
            /*
                  - GUILD_INTEGRATIONS_UPDATE
                  - INTEGRATION_CREATE
                  - INTEGRATION_UPDATE
                  - INTEGRATION_DELETE
             */
            GuildWebhooks = 1 << 5,
            /*
                  - WEBHOOKS_UPDATE
             */
            GuildInvites = 1 << 6,
            /*
                  - INVITE_CREATE
                  - INVITE_DELETE
             */
            GuildVoiceStates = 1 << 7,
            /*
                  - VOICE_STATE_UPDATE
             */
            GuildPrecenses = 1 << 8,
            /*
                  - PRESENCE_UPDATE
             */
            GuildMessages = 1 << 9,
            /*
                  - MESSAGE_CREATE
                  - MESSAGE_UPDATE
                  - MESSAGE_DELETE
                  - MESSAGE_DELETE_BULK
             */
            GuildMessageReactions = 1 << 10,
            /*
                  - MESSAGE_REACTION_ADD
                  - MESSAGE_REACTION_REMOVE
                  - MESSAGE_REACTION_REMOVE_ALL
                  - MESSAGE_REACTION_REMOVE_EMOJI
             */
            GuildMessageTyping = 1 << 11,
            /*
                  - TYPING_START
             */
            DirectMessages = 1 << 12,
            /*
                  - MESSAGE_CREATE
                  - MESSAGE_UPDATE
                  - MESSAGE_DELETE
                  - CHANNEL_PINS_UPDATE
             */
            DirectMessageReactions = 1 << 13,
            /*
                  - MESSAGE_REACTION_ADD
                  - MESSAGE_REACTION_REMOVE
                  - MESSAGE_REACTION_REMOVE_ALL
                  - MESSAGE_REACTION_REMOVE_EMOJI
             */
            DirectMessageTyping = 1 << 14,
            /*
                  - TYPING_START
             */
            MessageContent = 1 << 15,

            GuildScheduledEvents = 1 << 16,
            /*
                  - GUILD_SCHEDULED_EVENT_CREATE
                  - GUILD_SCHEDULED_EVENT_UPDATE
                  - GUILD_SCHEDULED_EVENT_DELETE
                  - GUILD_SCHEDULED_EVENT_USER_ADD
                  - GUILD_SCHEDULED_EVENT_USER_REMOVE
             */
            AutomodConfig = 1 << 17,
            /*
                  - AUTO_MODERATION_RULE_CREATE
                  - AUTO_MODERATION_RULE_UPDATE
                  - AUTO_MODERATION_RULE_DELETE
             */
            AutomodExecute = 1 << 18
            /*
                  - AUTO_MODERATION_ACTION_EXECUTION
            */
        }
    }

    internal sealed class GatewayEvent<PayloadType> : GatewayEventBase 
        where PayloadType : class, EventDataBase
    {
        [JsonConstructor]
        public GatewayEvent(int opCode, int? seqNumber, string? eventName, PayloadType? payload) : base(opCode, seqNumber, eventName)
        {
            this.Payload = payload;
        }
        public GatewayEvent(GatewayCode.OpCodes opCode) : base((int)opCode, null, null)
        {   }
        public GatewayEvent(GatewayCode.OpCodes opCode, PayloadType? payload) : this((int)opCode, null, null, payload)
        {   }

        [JsonPropertyName("d")]
        public PayloadType? Payload { get; set; }
    }

    internal sealed class GatewayDispatch<PayloadType> : GatewayEventBase
        where PayloadType : HttpModelBase
    {
        public GatewayDispatch(PayloadType payload) : base((int)GatewayCode.OpCodes.Dispatch, null, null)
        {
            Payload = payload;
        }

        public PayloadType Payload { get; set; }
    }

    internal sealed class GatewayHeartbeatEvent : GatewayEventBase
    {
        public GatewayHeartbeatEvent() : base((int)GatewayCode.OpCodes.Heartbeat, null, null)
        {   }
        public GatewayHeartbeatEvent(int? payload) : this()
        {
            this.Payload = payload;
        }

        [JsonPropertyName("d")]
        public int? Payload { get; set; }
    }

    internal sealed class GatewayNoRespEvent : GatewayEventBase 
    {
        [JsonConstructor]
        public GatewayNoRespEvent(int opCode, int? seqNumber, string? eventName) : base(opCode, seqNumber, eventName)
        {   }
        public GatewayNoRespEvent(GatewayCode.OpCodes input = GatewayCode.OpCodes.NoResponse) : base((int)input, null, null)
        {   }
    }

    internal class ReconnectException : Exception
    {   }
}
