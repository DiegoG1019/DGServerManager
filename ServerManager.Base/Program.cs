using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Buffers.Binary;
using DiegoG.ServerManager.Interprocess;
using DiegoG.ServerManager.Daemon;
using DiegoG.ServerManager.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using DiegoG.Utilities.Settings;
using DiegoG.Utilities.IO;
using Serilog;
using System.Runtime.Loader;
using System.Diagnostics;
using DiegoG.CLI;
using System.Runtime.Versioning;

namespace DiegoG.ServerManager.Interprocess
{
    [SupportedOSPlatform("Linux")]
    public class Program
    {
        static readonly string MutexName = "DiegoG.ServerManager.DaemonService.Main.AppMutex";
        static Serilog.Core.LoggingLevelSwitch LoggingLevelSwitch { get; } = new(Serilog.Events.LogEventLevel.Verbose);
        static CancellationTokenSource CancellationSource { get; set; }
        public static Task InteractiveTask { get; set; }

        [MTAThread]
        public static async Task Main(string[] args)
        {
            /*-----------------------------------Initialization-------------------------------------*/
            Stopwatch stopwatch = new();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.LocalSyslog("DGServerManager")
                .CreateLogger();

            //----------------------------If Exists, Daemon is already running
            if (Mutex.TryOpenExisting(MutexName, out Mutex _))
            {
                Log.Debug("Opening Interprocess Stream");
                InterprocessStream.Client.Init();
                if (args.Length > 1) // If so, simply feed them in and exit
                {
                    Log.Debug("Arguments to be passed are present, passing and exiting");
                    Log.Information("Writing message to interprocess stream");
                    InterprocessStream.Client.Write(args, false);
                    Log.Information("Messaged passed to Daemon");
                    Console.WriteLine("Messaged Passed.");
                    Log.Debug("Closing Interprocess Stream");
                    InterprocessStream.Client.Terminate();
                    return;
                }

                //----------------------------Interactive
                CancellationSource = new();
                AssemblyLoadContext.Default.Unloading += a =>
                {
                    Log.Information("Exiting Interactive");
                    CancellationSource.Cancel();
                    var ch = new Stopwatch(); ch.Start();
                    if (!InteractiveTask.Wait(TimeSpan.FromSeconds(10)))
                    {
                        InterprocessStream.Client.Terminate();
                        Log.Fatal("Application took more than 10 seconds to exit, forcing termination");
                        throw new ApplicationException("Application took more than 10 seconds to exit, forcing termination");
                    }
                    ch.Stop();
                    InterprocessStream.Client.Terminate();
                    Log.Information($"Application exited gracefully within {ch.Elapsed.TotalSeconds} seconds");
                };

                stopwatch.Start();
                LoadApp();

                Log.Information("Loading DGServerManager.Interactive Settings");
                Settings<InteractiveSettings>.Initialize(Directories.Settings, "DGServerManager.Interactive.Settings");

                Log.Information("Settings:");
                foreach (var p in Settings<InteractiveSettings>.CurrentProperties)
                    Log.Information($"Setting \"{p.ObjectA}\" = {p.ObjectB}");

                Log.Information($"Started \"DGServerManager.Interactive\" @ {DateTime.Now} within {stopwatch.Elapsed.TotalSeconds} seconds");
                stopwatch.Stop(); stopwatch = null;

                InteractiveTask = InteractiveProcess.Run(args, CancellationSource.Token);
                await InteractiveTask;
                throw new ApplicationException("This point in code should not have been reached.");
            }

            //----------------------------Daemon
            stopwatch.Start();
            LoadApp();
            using Mutex AppMutex = new(true, MutexName, out bool createdNew);
            if (!createdNew)
                throw new Exception("Could not open an existing Mutex or create a new one.");

            InterprocessStream.Daemon.Init();

            Log.Information("Loading DGServerManager.Daemon Settings");
            Settings<DaemonSettings>.Initialize(Directories.Settings, "DGServerManager.Daemon.Settings");

            Log.Information("Settings:");
            foreach (var p in Settings<DaemonSettings>.CurrentProperties)
                Log.Information($"Setting \"{p.ObjectA}\" = {p.ObjectB}");

            Log.Information("Initializing Extension directories");
            Directories.InitOtherDirectories();

            Log.Information("Initializing Command Processor");
            Commands.Initialize(new(true, false, false, false, false, true));

            DaemonProcess.Args = Commands.FullSplit(args);

            Log.Information($"Started \"DGServerManager.Daemon\" @ {DateTime.Now} within {stopwatch.Elapsed.TotalSeconds}");
            stopwatch.Stop(); stopwatch = null;

            await CreateHostBuilder(args).Build().RunAsync();
            var t = Settings<DaemonSettings>.SaveSettingsAsync();
            Settings<BaseSettings>.SaveSettings(); await t;
            InterprocessStream.Daemon.Terminate();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) 
            => Host.CreateDefaultBuilder(args).ConfigureServices((hostContext, services) => services.AddHostedService<Worker>());

        public static void LoadApp()
        {
            Log.Information("Initializing all application directories");
            Directories.InitApplicationDirectories();

            Log.Debug("Initializing Serialization settings");
            Serialization.Init();

            Log.Information("Loading App Settings");
            Settings<BaseSettings>.Initialize(Directories.Settings, "DGServerManager.Base.Settings");

            Log.Debug("Hooking LoggingLevelSwitch to Settings<AppSettings>.Current.Verbosity");
            LoggingLevelSwitch.MinimumLevel = Settings<BaseSettings>.Current.Verbosity;
            Settings<BaseSettings>.SettingsChanged += (s, e) => LoggingLevelSwitch.MinimumLevel = Settings<BaseSettings>.Current.Verbosity;

            Log.Information($"Succesfully loaded AppSettings, set logging to a mimum level of {Enum.GetName(typeof(Serilog.Events.LogEventLevel), LoggingLevelSwitch.MinimumLevel)}");

            Log.Information("Settings:");
            foreach (var p in Settings<BaseSettings>.CurrentProperties)
                Log.Information($"Setting \"{p.ObjectA}\" = {p.ObjectB}");
        }
    }
}
