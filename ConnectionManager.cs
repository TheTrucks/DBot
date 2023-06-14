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

namespace DBot
{
    public sealed class ConnectionOptions
    {
        public required string HttpAddress { get; set; }
    }

    public sealed class ConnectionManager : IHostedService
    {
        private ClientWebSocket CWS = new ClientWebSocket();
        private ConnectionOptions _opts;
        private EventProcessor _proc;
        ILogger<ConnectionManager> _logger;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? BackgroundWorker;
        private Task? HeartbeatWorker;
        private PeriodicTimer? _heartbeatTimer;
        private int? _lastSeq;
        private string? WSSAddress;

        public ConnectionManager(IOptions<ConnectionOptions> opts, ILogger<ConnectionManager> logger, EventProcessor proc)
        {
            _opts = opts.Value;
            _proc = proc;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken _token)
        {
            await ConnectAsync(_token);
            BackgroundWorker = Task.Factory.StartNew(WorkingLoop);
        }

        private async Task ConnectAsync(CancellationToken _token)
        {
            if (CWS.State is not WebSocketState.Open)
            {
                if (WSSAddress is null)
                    throw new Exception("WSS address wasn't resolved");
                _logger.LogInformation("Opening WSS connection");
                await CWS.ConnectAsync(new Uri(WSSAddress), _token);
                _logger.LogInformation("WSS connected");
            }
        }

        private async Task Heartbeat()
        {
            var _token = _cts.Token;
            while (!_token.IsCancellationRequested) 
            {
                if (_heartbeatTimer is null)
                {
                    _logger.LogError("Heartbeat timer wasn't properly initialized, resuming in 1s");
                    await Task.Delay(1000);
                }
                else
                {
                    await _heartbeatTimer.WaitForNextTickAsync(_token);
                    _logger.LogInformation("Heartbeat out, seq: {0}", _lastSeq.HasValue ? _lastSeq.Value : "null");
                    var heartbeat = new GatewayEvent<int?>(GatewayEventBase.OpCodes.Heartbeat, _lastSeq);
                    await SendAnswer(heartbeat, _token);
                    _logger.LogInformation("Heartbeaten");
                }
            }
        }

        private async Task<(IMemoryOwner<byte> Data, int DataSize)> ReceiveEventData(CancellationToken _token)
        {
            int DataSize = 0;
            int memSize = 4096;
            var EventData = MemoryPool<byte>.Shared.Rent(memSize);

            try
            {
                var result = await CWS.ReceiveAsync(EventData.Memory, _token);
                DataSize += result.Count;

                _logger.LogInformation("Data receiving");
                while (!result.EndOfMessage || !_token.IsCancellationRequested)
                {
                    _logger.LogInformation("Data receiving buffer increase");
                    memSize *= 2;
                    var tmpRes = MemoryPool<byte>.Shared.Rent(memSize);
                    EventData.Memory.CopyTo(tmpRes.Memory);
                    EventData.Dispose();
                    EventData = tmpRes;

                    result = await CWS.ReceiveAsync(EventData.Memory, _token);
                    DataSize += result.Count;
                }
                _logger.LogInformation("Data received: {0}KB", (double)result.Count / 1024);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data receive error with memSize: {0}", memSize);
            }
            finally
            {
                EventData.Dispose();
            }

            return (EventData, DataSize);
        }

        private async Task SendAnswer(GatewayEventBase Answer, CancellationToken _token)
        {
            _logger.LogInformation("Sending answer of type {0}", Answer.GetType().Name);
            using (var DataMS = new MemoryStream()) 
            {
                JsonSerializer.Serialize(DataMS, Answer);
                using (var DataMemory = MemoryPool<byte>.Shared.Rent((int)DataMS.Length))
                {
                    DataMS.Seek(0, SeekOrigin.Begin);
                    int read = await DataMS.ReadAsync(DataMemory.Memory, _token);
                    await CWS.SendAsync(DataMemory.Memory.Slice(0, read), WebSocketMessageType.Binary, true, _token);
                }
            }
            _logger.LogInformation("Answer sent");
        }

        private async Task InitHeartbeat(int intervalms)
        {

        }

        private async Task WorkingLoop()
        {
            var _token = _cts.Token;

            while (!_token.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(_token);
                    var AnswerData = await ReceiveEventData(_token);
                    var Answer = _proc.ProcessEvent(AnswerData.Data, AnswerData.DataSize, _token);
                    switch (Answer.OpCode)
                    {
                        case (int)GatewayEventBase.OpCodes.Hello:
                            await InitHeartbeat(((GatewayEvent<Hello>)Answer).Payload!.HeartbeatInterval);
                            break;
                        default:
                            await SendAnswer(Answer, _token);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Service working loop error");
                    _logger.LogInformation("Service working loop broke; restarting");
                }
            }
        }

        public async Task StopAsync(CancellationToken _token)
        {
            _logger.LogInformation("Stopping service");
            _cts.Cancel();
            if (CWS.State is WebSocketState.Open)
            {
                await CWS.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stop", _token);
            }
            if (HeartbeatWorker is not null && !HeartbeatWorker.IsCompleted)
                HeartbeatWorker.Dispose();
            if (BackgroundWorker is not null && !BackgroundWorker.IsCompleted)
                BackgroundWorker.Dispose();
            _heartbeatTimer?.Dispose();
            CWS.Dispose();
            _logger.LogInformation("Service stopped");
        }
    }
}
