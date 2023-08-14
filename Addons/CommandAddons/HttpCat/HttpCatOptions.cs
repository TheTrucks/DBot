using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBot.Addons.CommandAddons.HttpCat
{
    internal sealed class HttpCatOptions
    {
        public required string BaseAddress { get; set; }
        public string? Suffix { get; set; }
    }
}
