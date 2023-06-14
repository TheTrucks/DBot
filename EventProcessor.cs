using DBot.Models;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBot
{
    public sealed class EventProcessor
    {
        public async GatewayEventBase ProcessEvent(IMemoryOwner<byte> data, int dataSize, CancellationToken _token)
        {

        }


    }
}
