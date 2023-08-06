using DBot.Models.EventData;
using DBot.Models;
using DBot.Processing;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBot.Services
{
    internal sealed class RequestManager : IDisposable
    {
        private readonly ILogger<RequestManager> _logger;
        private readonly ConnectionManager _connectionManager;
        private readonly EventProcessorManager _proc;
        private readonly SenderService _sender;

        private PeriodicTimer? _heartbeatTimer;
        private Task? HeartbeatWorker;
        private Task? BackgroundWorker;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public RequestManager(ILogger<RequestManager> logger, ConnectionManager connectionManager, EventProcessorManager proc, SenderService sender) 
        {
            _logger = logger;
            _connectionManager = connectionManager;
            _proc = proc;
            _sender = sender;
        }

        public Task Initialize()
        {
            BackgroundWorker = Task.Factory.StartNew(WorkingLoop);
            return Task.CompletedTask;
        }

        private async Task ProcessRequest(IMemoryOwner<byte> Data, int DataSize)
        {
            var _token = _cts.Token;

            using (Data)
            {
                var Answer = await _proc.ProcessEvent(Data, DataSize);

                switch (GatewayCode.GetOpCode(Answer.OpCode))
                {
                    case GatewayCode.OpCodes.Hello:
                        InitHeartbeat(((GatewayEvent<Hello>)Answer).Payload!.HeartbeatInterval);
                        await _sender.SendAnswer(_connectionManager.CWS, _proc.CreateIdentity(), _token);
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
                    case GatewayCode.OpCodes.Dispatch:
                        await _sender.SendWebhook(Answer, _token);
                        break;
                    default:
                        await _sender.SendAnswer(_connectionManager.CWS, Answer, _token);
                        break;
                }
            }
        }

        private async Task WorkingLoop()
        {
            var _token = _cts.Token;

            while (!_token.IsCancellationRequested)
            {
                try
                {
                    await _connectionManager.ConnectAsync(false, GetReconnectData(true), _token);
                    var AnswerData = await _connectionManager.ReceiveEventData(_token);
                    await ProcessRequest(AnswerData.Data, AnswerData.DataSize);
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

        private (Uri resumeGateway, string sessionId)? GetReconnectData(bool clear)
        {
            return _proc.GetReconnectData(clear);
        }

        private void InitHeartbeat(int intervalms)
        {
            if (HeartbeatWorker is not null && !HeartbeatWorker.IsCompleted)
                HeartbeatWorker.Dispose();
            _heartbeatTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalms));
            HeartbeatWorker = Task.Factory.StartNew(Heartbeat);
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
                    var heartbeat = await _proc.CreateHeartbeat();
                    _logger.LogTrace("Heartbeat out, seq: {seq}", heartbeat.Payload.HasValue ? heartbeat.Payload.Value : "null");
                    await _sender.SendAnswer(_connectionManager.CWS, heartbeat, _token);
                    _logger.LogDebug("Heartbeaten");
                }
            }
        }

        public void Dispose()
        {
            if (HeartbeatWorker is not null && !HeartbeatWorker.IsCompleted)
                HeartbeatWorker.Dispose();

            _heartbeatTimer?.Dispose();

            _connectionManager.Dispose();
        }
    }
}
