using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.EventData
{
    internal sealed class Identity : EventDataBase
    {
        public Identity(string token, int intents, string os, string browser, string device) 
        {
            Token = token;
            Intents = intents;
            Properties = new Properties(os, browser, device);
        }

        [JsonPropertyName("token")]
        public string Token { get; set; }

        [JsonPropertyName("intents")]
        public int Intents { get; set; }

        [JsonPropertyName("properties")]
        public Properties Properties { get; set; }
    }
    internal sealed class Properties
    {
        public Properties(string os, string browser, string device) 
        {
            OS = os;
            Browser = browser;
            Device = device;
        }
        [JsonPropertyName("os")]
        public string OS { get; set; }

        [JsonPropertyName("browser")]
        public string Browser { get; set; }

        [JsonPropertyName("device")]
        public string Device { get; set; }
    }
}
