using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.HttpModels.Interaction
{
    internal class InteractionMessage : InteractionResponsePayload
    {
        public InteractionMessage(string? content, InteractionFlags? flags)
        {
            Content = content;
            if (flags.HasValue)
                Flags = (int)flags.Value;
        }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("flags")]
        public int? Flags { get; set; }



        [Flags]
        public enum InteractionFlags
        {
            SUPRESS_EMBEDS = 1 << 2,
            EPHEMERAL = 1 << 6
        }
    }
}
