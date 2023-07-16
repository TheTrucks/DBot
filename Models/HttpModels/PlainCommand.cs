using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DBot.Models.HttpModels
{
    internal class PlainCommand : HttpModelBase
    {
        public PlainCommand(HttpMethod method, string uri, string payload) : base(method, uri, 0)
        {
            Payload = payload;
        }

        [JsonIgnore]
        public string Payload { get; set; }
    }
}
