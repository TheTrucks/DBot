using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.EventData
{
    public sealed class GetGateway
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
