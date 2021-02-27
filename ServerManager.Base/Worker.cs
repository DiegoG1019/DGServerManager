using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Interprocess
{
    public class Worker : BackgroundService
    {
        public Worker(ILogger<Worker> logger)
        { }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) => await Daemon.DaemonProcess.Run(stoppingToken);
    }
}
