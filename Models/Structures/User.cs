using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.Structures
{
    internal sealed class User
    {
        public User(string id, string name, string discriminator, string? displayName)
        {
            Id = id;
            Name = name;
            Discriminator = discriminator;
            DisplayName = displayName;
        }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("username")]
        public string Name { get; set; }

        [JsonPropertyName("discriminator")]
        public string Discriminator { get; set; }

        [JsonPropertyName("global_name")]
        public string? DisplayName { get; set; }
    }
}
