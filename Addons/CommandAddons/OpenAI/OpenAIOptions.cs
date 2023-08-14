using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBot.Addons.CommandAddons.OpenAI
{
    internal sealed class OpenAIOptions
    {
        public required string BaseAddress { get; set; }
        public required string Token { get; set; }
        public required string Model { get; set; }
    }
}
