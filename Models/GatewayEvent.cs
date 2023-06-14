using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models
{
    internal abstract class GatewayEventBase
    {
        [JsonPropertyName("op")]
        public int OpCode { get; set; }

        [JsonPropertyName("s")]
        public int? SeqNumber { get; set; }

        [JsonPropertyName("t")]
        public string? EventName { get; set; }

        public enum OpCodes
        {
            Ready = 0,
            Heartbeat = 1,
            Identity = 2,
            Resume = 6,
            Reconnect = 7,
            InvalidSession = 9,
            Hello = 10,
            HeartbeatAck = 11
        }

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
    {
        public GatewayEvent(OpCodes opcode)
        {
            this.OpCode = (int)opcode;
        }
        public GatewayEvent(OpCodes opcode, PayloadType payload) : this(opcode)
        {
            this.Payload = payload;
        }

        [JsonPropertyName("d")]
        public PayloadType? Payload { get; set; }
    }
}
