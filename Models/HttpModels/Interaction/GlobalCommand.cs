using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static DBot.Models.EventData.Interaction;

namespace DBot.Models.HttpModels.Interaction
{
    internal class GlobalCommands<InteractionData> : HttpModelBase
    {
        public GlobalCommands(GlobalCommand<InteractionData>[] payload) : base(HttpMethod.Put, "applications/{0}/commands", 1)
        {
            Payload = payload;
        }

        public GlobalCommand<InteractionData>[] Payload { get; set; }
    }

    internal class GlobalCommand<InteractionData>
    {
        public GlobalCommand(string name, string? description, InteractionData[]? options)
        {
            Name = name;
            Description = description;
            Options = options;
        }

        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("options")]
        public InteractionData[]? Options { get; init; }
    }
}
