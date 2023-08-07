using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using System.Buffers;
using DBot.Models;
using System.Text.Json;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DBot.Models.EventData;
using DBot.Processing;
using DBot.Models.Options;

namespace DBot.Services
{
    internal sealed class ConnectionManager : IDisposable
    {
        public ClientWebSocket CWS = new ClientWebSocket();
        private AppOptions _opts;

        private readonly ILogger<ConnectionManager> _logger;
        private string? WSSAddress;
        private bool _disposed = false;

        public ConnectionManager(IOptions<AppOptions> opts, ILogger<ConnectionManager> logger)
        {
            _opts = opts.Value;
            _logger = logger;

            WSSAddress = _opts.WSSAddress;
        }

        public async Task ConnectAsync(bool forceReconnect, (Uri resumeGateway, string sessionId)? reconnectData, CancellationToken _token)
        {
            if (CWS.State is not WebSocketState.Closed)
            {
                if (forceReconnect || CWS.State is not WebSocketState.Open)
                {
                    _logger.LogInformation("Closing present WSS connection");
                    await CWS.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _token);
                    CWS.Dispose();
                    CWS = new ClientWebSocket();
                }
                else
                    return;
            }

            if (reconnectData is null)
            {
                if (WSSAddress is null)
                    throw new Exception("WSS address wasn't resolved");
                _logger.LogInformation("Opening WSS connection");
                CWS.Dispose();
                CWS = new ClientWebSocket();
                await CWS.ConnectAsync(new Uri(WSSAddress), _token);
                _logger.LogInformation("WSS connected");
            }
            else
            {
                _logger.LogTrace("Reconnection data is present. Address {addr}, session {session}", reconnectData.Value.resumeGateway, reconnectData.Value.sessionId);
                CWS.Dispose();
                CWS = new ClientWebSocket();
                await CWS.ConnectAsync(reconnectData.Value.resumeGateway, _token);
                _logger.LogInformation("WSS reconnected");
            }
        }

        public async Task<(IMemoryOwner<byte> Data, int DataSize)> ReceiveEventData(CancellationToken _token)
        {
            int DataSize = 0;
            int memSize = 4096;
            var EventData = MemoryPool<byte>.Shared.Rent(memSize);

            try
            {
                while (DataSize == 0)
                {
                    var result = await CWS.ReceiveAsync(EventData.Memory, _token);
                    DataSize += result.Count;

                    _logger.LogDebug("Data receiving");
                    while (!result.EndOfMessage && !_token.IsCancellationRequested)
                    {
                        _logger.LogDebug("Data receiving buffer increase");
                        memSize *= 2;
                        var tmpRes = MemoryPool<byte>.Shared.Rent(memSize);
                        EventData.Memory.CopyTo(tmpRes.Memory);
                        EventData.Dispose();
                        EventData = tmpRes;

                        result = await CWS.ReceiveAsync(EventData.Memory, _token);
                        DataSize += result.Count;
                    }
                    _logger.LogTrace("Data received: {size}KB", (double)result.Count / 1024);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data receive error with memSize: {size}", memSize);
            }

            return (EventData, DataSize);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (CWS.State is not WebSocketState.Closed)
                {
                    CWS.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stop", CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
                    CWS.Dispose();
                }
            }
        }
    }
}
