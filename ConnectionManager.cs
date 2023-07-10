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

namespace DBot
{
    internal sealed class ConnectionOptions
    {
        public required string HttpAddress { get; set; }
    }

    internal sealed class ConnectionManager : IHostedService
    {
        private ClientWebSocket CWS = new ClientWebSocket();
        private ConnectionOptions _opts;
        private EventProcessorManager _proc;
        ILogger<ConnectionManager> _logger;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? BackgroundWorker;
        private Task? HeartbeatWorker;
        private PeriodicTimer? _heartbeatTimer;
        private string? WSSAddress;

        public ConnectionManager(IOptions<ConnectionOptions> opts, ILogger<ConnectionManager> logger, EventProcessorManager proc)
        {
            _opts = opts.Value;
            _proc = proc;
            _logger = logger;

            WSSAddress = _opts.HttpAddress;
        }

        public Task StartAsync(CancellationToken _token)
        {
            BackgroundWorker = Task.Factory.StartNew(WorkingLoop);
            return Task.CompletedTask;
        }

        private async Task ConnectAsync(bool reconnect, CancellationToken _token)
        {
            if (CWS.State is WebSocketState.Open)
            {
                if (reconnect)
                {
                    _logger.LogInformation("Closing present WSS connection");
                    await CWS.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _token);
                    CWS.Dispose();
                    CWS = new ClientWebSocket();
                }
                else
                    return;
            }

            var ReconnectData = _proc.GetReconnectData(true);
            if (ReconnectData is null)
            {
                if (WSSAddress is null)
                    throw new Exception("WSS address wasn't resolved");
                _logger.LogInformation("Opening WSS connection");
                await CWS.ConnectAsync(new Uri(WSSAddress), _token);
                _logger.LogInformation("WSS connected");
            }
            else
            {
                _logger.LogTrace("Reconnection data is present. Address {addr}, session {session}", ReconnectData.Value.resumeGateway, ReconnectData.Value.sessionId);
                await CWS.ConnectAsync(ReconnectData.Value.resumeGateway, _token);
                _logger.LogInformation("WSS reconnected");
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
                    var heartbeat = _proc.CreateHeartbeat();
                    _logger.LogTrace("Heartbeat out, seq: {0}", heartbeat.Payload.HasValue ? heartbeat.Payload.Value : "null");
                    await SendAnswer(heartbeat, _token);
                    _logger.LogDebug("Heartbeaten");
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
                    _logger.LogTrace("Data received: {0}KB", (double)result.Count / 1024);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data receive error with memSize: {0}", memSize);
            }
            
            return (EventData, DataSize);
        }

        private async Task SendAnswer(GatewayEventBase Answer, CancellationToken _token)
        {
            _logger.LogTrace("Sending answer of type {0}", GatewayCode.GetOpCode(Answer.OpCode).ToString());
            using (var DataMS = new MemoryStream()) 
            {
                JsonSerializer.Serialize(DataMS, Answer, Answer.GetType());
                using (var DataMemory = MemoryPool<byte>.Shared.Rent((int)DataMS.Length))
                {
                    DataMS.Seek(0, SeekOrigin.Begin);
                    int read = await DataMS.ReadAsync(DataMemory.Memory, _token);
                    await CWS.SendAsync(DataMemory.Memory.Slice(0, read), WebSocketMessageType.Binary, true, _token);
                }
            }
            _logger.LogDebug("Answer sent");
        }

        private void InitHeartbeat(int intervalms)
        {
            if (HeartbeatWorker is not null && !HeartbeatWorker.IsCompleted)
                HeartbeatWorker.Dispose();
            _heartbeatTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalms));
            HeartbeatWorker = Task.Factory.StartNew(Heartbeat);
        }

        private async Task WorkingLoop()
        {
            var _token = _cts.Token;

            while (!_token.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(false, _token);
                    var AnswerData = await ReceiveEventData(_token);
                    using (AnswerData.Data)
                    {
                        var Answer = _proc.ProcessEvent(AnswerData.Data, AnswerData.DataSize);

                        switch (GatewayCode.GetOpCode(Answer.OpCode))
                        {
                            case GatewayCode.OpCodes.Hello:
                                InitHeartbeat(((GatewayEvent<Hello>)Answer).Payload!.HeartbeatInterval);
                                await SendAnswer(_proc.CreateIdentity(), _token);
                                break;
                            case GatewayCode.OpCodes.HeartbeatAck:
                                //ResetReconnectTimer();
                                break;
                            case GatewayCode.OpCodes.Reconnect:
                                throw new ReconnectException();
                            case GatewayCode.OpCodes.InvalidSession:
                                throw new Exception("Session is considered invalid");
                            case GatewayCode.OpCodes.NoResponse:
                                break;
                            default:
                                await SendAnswer(Answer, _token);
                                break;
                        }
                    }
                }
                catch (ReconnectException)
                {
                    _logger.LogInformation("Reconnect issued");
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
