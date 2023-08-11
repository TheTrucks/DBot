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
using DBot.Models.Options;
using Microsoft.Extensions.Options;
using DBot.Processing.Processors;
using DBot.Models.HttpModels.Interaction;

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
        private HandlerOptions _opts { get; set; }
        private SemaphoreSlim _threadLimiter;
        private bool _disposed = false;
        private bool _reconState = false;
        private bool ForceReconnect
        {
            get
            {
                if (_reconState)
                {
                    _reconState = false;
                    return true;
                }
                return false;
            }
            set { _reconState = value; }
        }

        private int? _lastSeq;
        private string? _sessionId;
        private Uri? _reconnectUri;

        public RequestManager(IOptions<HandlerOptions> opts, ILogger<RequestManager> logger, ConnectionManager connectionManager, EventProcessorManager proc, SenderService sender) 
        {
            _opts = opts.Value;
            _logger = logger;
            _connectionManager = connectionManager;
            _proc = proc;
            _sender = sender;
            _threadLimiter = new SemaphoreSlim(_opts.ThreadFactor, _opts.ThreadFactor);
        }

        public Task Initialize()
        {
            BackgroundWorker = Task.Factory.StartNew(WorkingLoop);
            return Task.CompletedTask;
        }

        private async Task ProcessRequest(IMemoryOwner<byte> Data, int DataSize)
        {
            await _threadLimiter.WaitAsync();
            try
            {
                var _token = _cts.Token;

                using (Data)
                {
                    var Answer = await _proc.ProcessEvent(Data, DataSize);
                    if (Answer.SeqNumber is not null)
                        _lastSeq = Answer.SeqNumber;

                    switch (GatewayCode.GetOpCode(Answer.OpCode))
                    {
                        case GatewayCode.OpCodes.Hello:
                            InitHeartbeat(((GatewayEvent<Hello>)Answer).Payload!.HeartbeatInterval);
                            await _sender.SendAnswer(_connectionManager.CWS, _proc.CreateIdentity(), _token);
                            break;
                        case GatewayCode.OpCodes.Heartbeat:
                            await _sender.SendAnswer(_connectionManager.CWS, await SystemEventsProcessor.CreateHeartbeat(_lastSeq), _token);
                            break;
                        case GatewayCode.OpCodes.Ready:
                            ReadReconnectInfo(Answer);
                            await _sender.SendWebhook(Answer, _token);
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
            finally
            {
                _threadLimiter.Release();
            }
        }

        private async Task WorkingLoop()
        {
            var _token = _cts.Token;

            while (!_token.IsCancellationRequested)
            {
                try
                {
                    await _connectionManager.ConnectAsync(ForceReconnect, _reconnectUri, _sessionId, _token);
                    var AnswerData = await _connectionManager.ReceiveEventData(_token);
                    _ = Task.Factory.StartNew(() => ProcessRequest(AnswerData.Item1, AnswerData.Item2), _token);
                }
                catch (ReconnectException)
                {
                    ForceReconnect = true;
                    _logger.LogInformation("Reconnect issued");

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Service working loop error");
                    _logger.LogInformation("Service working loop broke; restarting");
                }
            }
        }

        private void ReadReconnectInfo(GatewayEventBase input)
        {
            var inputCast = input as GatewayDispatch<ReadyInfo<Interaction.AppCommandInteractionOption>>;
            if (inputCast != null)
            {
                _reconnectUri = inputCast.Payload.ResumeGateway;
                _sessionId = inputCast.Payload.SessionId;
            }
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
                    var heartbeat = await SystemEventsProcessor.CreateHeartbeat(_lastSeq);
                    _logger.LogTrace("Heartbeat out, seq: {seq}", heartbeat.Payload.HasValue ? heartbeat.Payload.Value : "null");
                    await _sender.SendAnswer(_connectionManager.CWS, heartbeat, _token);
                    _logger.LogDebug("Heartbeaten");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _cts.Cancel();
                _cts.Dispose();

                if (HeartbeatWorker is not null && !HeartbeatWorker.IsCompleted)
                    HeartbeatWorker.Dispose();
                if (BackgroundWorker is not null && !BackgroundWorker.IsCompleted)
                    BackgroundWorker.Dispose();

                _heartbeatTimer?.Dispose();

                _connectionManager.Dispose();
            }
        }
    }
}
