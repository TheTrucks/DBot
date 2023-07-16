using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.HttpModels.Interaction
{
    internal sealed class InteractionResponse<Payload> : HttpModelBase
    {
        public InteractionResponse(CallbackType type, Payload? data, string interactionId, string interactionToken) : base(HttpMethod.Post, $"interactions/{interactionId}/{interactionToken}/callback", 0)
        {
            Type = type;
            Data = data;
        }

        [JsonPropertyName("type")]
        public CallbackType Type { get; set; }

        [JsonPropertyName("data")]
        public Payload? Data { get; set; }

        public enum CallbackType
        {
            PONG = 1,
            CHANNEL_MESSAGE_WITH_SOURCE = 4,
            DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE = 5,
            DEFERRED_UPDATE_MESSAGE = 6,
            UPDATE_MESSAGE = 7,
            APPLICATION_COMMAND_AUTOCOMPLETE_RESULT = 8,
            MODAL = 9
        }
    }

    public interface InteractionResponsePayload { }
}
