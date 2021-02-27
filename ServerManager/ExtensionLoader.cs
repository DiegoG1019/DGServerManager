using DiegoG.Utilities.Settings;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DiegoG.Utilities.Collections;
using DiegoG.CLI;
using System.Diagnostics;

namespace DiegoG.ServerManager.Daemon
{
    public static class ExtensionLoader
    {
        private static AppDomain ExtensionsDomain { get; } = AppDomain.CreateDomain("DiegoG.ServerManager.Daemon.Extensions");
        private static List<string> LoadedExtensions { get; } = new();
        public static void Load()
        {

            for(int i = 0; i < 1000; i++)
            {
                if (i % 50 != 0) 
                    continue;
                Console.WriteLine("Found a multiple of 50!: " + i);
            }

            var count = 0;
            var ch = new Stopwatch();
            ch.Start();
            Log.Information("Reloading extensions");
            foreach 
            (
                var file in 
                Directory.EnumerateFiles(Settings<DaemonSettings>.Current.ExtensionDir, "*.dll", SearchOption.AllDirectories)
                .Where(s=>!LoadedExtensions.Contains(s))
            )
            {
                Log.Debug("Loading Assembly: " + file);
                var asm = Assembly.LoadFrom(file);
                Log.Debug("Loading assembly into " + nameof(ExtensionsDomain));
                ExtensionsDomain.Load(asm.GetName(false));
                LoadedExtensions.Add(file);
                count++;
            }
            Log.Information("Registering newly loaded assemblies. Time Taken so far: " + ch.Elapsed);
            Log.Debug("Registering new commands");
            Commands.LoadCommands(ExtensionsDomain.GetAssemblies());
            Log.Information($"Loaded {count} new assemblies for a total of {LoadedExtensions.Count} extension assemblies. Total Time Taken: {ch.Elapsed}");
        }
    }
}
