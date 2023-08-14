using DBot.Addons.CommandAddons.HttpCat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBot.Addons
{
    internal static class DIRegisterExtension
    {
        public static void RegisterAddons(this IServiceCollection services, IConfiguration config)
        {
            HttpCatAddon.ConfigureAddon(services, config);
        }
    }
}
