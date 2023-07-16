using DBot.Models.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.HttpModels.Channel
{
    internal sealed class Message : HttpModelBase
    {
        public Message(string? content, MsgRef? messageReference) : base(HttpMethod.Post, "channels/{0}/messages", 1)
        {
            Content = content;
            MessageReference = messageReference;
        }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("message_reference")]
        public MsgRef? MessageReference { get; set; }
    }
}
