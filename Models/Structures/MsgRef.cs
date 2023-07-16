using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.Structures
{
    internal sealed class MsgRef
    {
        public MsgRef(string? messageId, string? channelId, string? guildId)
        {
            MessageId = messageId;
            ChannelId = channelId;
            GuildId = guildId;
        }
        [JsonPropertyName("message_id")]
        public string? MessageId { get; set; }

        [JsonPropertyName("channel_id")]
        public string? ChannelId { get; set; }

        [JsonPropertyName("guild_id")]
        public string? GuildId { get; set; }
    }
}
