using DBot.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace DBot.Services
{
    internal sealed class AppService : IHostedService
    {
        private readonly ILogger<AppService> _logger;
        private readonly RequestManager _requestManager;

        public AppService(ILogger<AppService> logger, RequestManager requestManager)
        {
            _logger = logger;
            _requestManager = requestManager;
        }

        public async Task StartAsync(CancellationToken _token)
        {
            await _requestManager.Initialize();
        }

        
        public Task StopAsync(CancellationToken _token)
        {
            _logger.LogInformation("Stopping service");

            _requestManager.Dispose();

            _logger.LogInformation("Service stopped");

            return Task.CompletedTask;
        }
    }
}
