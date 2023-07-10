using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.EventData
{
    internal class Ready : EventDataBase
    {
        public Ready(int version, string sessionId, string resumeGateway)
        {
            Version = version;
            SessionId = sessionId;
            ResumeGateway = resumeGateway;
        }

        [JsonPropertyName("v")]
        public int Version { get; set; }

        //[JsonPropertyName("user")]
        //UserObject User { get; set; }

        //[JsonPropertyName("guilds")]
        //Guild[] Guilds { get; set; }

        [JsonPropertyName("session_id")]
        public string SessionId { get; set; }

        [JsonPropertyName("resume_gateway_url")]
        public string ResumeGateway { get; set; }
    }

    //internal class UserObject
    //{

    //}

    //internal class Guild
    //{

    //}
}
