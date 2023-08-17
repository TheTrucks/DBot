using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Addons.CommandAddons.OpenAI
{
    internal sealed class GipityMessage
    {
        public enum AuthorRole
        {
            System,
            Assistant,
            User,
            Function
        }

        // todo - prob get rid of enums here later
        public GipityMessage(AuthorRole role, string? content)
        {
            Role = role.ToString().ToLowerInvariant();
            Content = content;
        }

        // todo - hide the constructor from outside, don't care rn
        [JsonConstructor]
        public GipityMessage(string role, string? content)
        {
            Role = role;
            Content = content;
        }

        public string Role { get; init; }
        public string? Content { get; set; }
    }

    internal sealed class GipityRequest
    {
        public GipityRequest(string model, List<GipityMessage> messages)
        {
            Model = model;
            Messages = messages;
        }

        public string Model { get; set; }
        public List<GipityMessage> Messages { get; set; }
    }

    internal sealed class GipityChoice
    {
        public int Index { get; set; }
        public GipityMessage? Message { get; set; }
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    internal sealed class GipityResponse
    {
        public List<GipityChoice>? Choices { get; set; }
    }
}
