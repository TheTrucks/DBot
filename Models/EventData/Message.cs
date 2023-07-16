using DBot.Models.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.EventData
{
    internal abstract class MessageEventBase : EventDataBase
    {
        public MessageEventBase(string id, string channelId, User? author, string content, DateTime timestamp, User[] mentions)
        {
            Id = id;
            ChannelId = channelId;
            Author = author;
            Content = content;
            Timestamp = timestamp;
            Mentions = mentions;
        }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("channel_id")]
        public string ChannelId { get; set; }

        [JsonPropertyName("author")]
        public User? Author { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("mentions")]
        public User[] Mentions { get; set; }
    }

    internal sealed class MessageEventCreate : MessageEventBase
    {
        public MessageEventCreate(string id, string channelId, User? author, string content, DateTime timestamp, User[] mentions, string? guildId) : base(id, channelId, author, content, timestamp, mentions)
        {
            GuildId = guildId;
        }

        [JsonPropertyName("guild_id")]
        public string? GuildId { get; set; }
    }
}
