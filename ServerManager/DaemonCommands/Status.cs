using DiegoG.CLI;
using DiegoG.Utilities.IO;
using DiegoG.Utilities.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.DaemonCommands
{
    [CLICommand]
    public class Stats : ICommand
    {
        public string HelpExplanation => "Obtains and returns the current status of the Daemon.";
        public string HelpUsage => "stats (--settings) (--json)";
        public string Trigger => "stats";
        public string Alias => null;
        public IEnumerable<(string, string)> HelpOptions { get; } = new[]
        {
            ("--json (-j)", "Instructs the Daemon to output statistics in JSON format"),
            ("--settings", "Outputs the current Daemon settings, always in JSON format")
        };
        public Task<string> Action(CommandArguments args)
            => Task.FromResult(
                args.Options.Contains("json") || args.Flags.Contains("j") ? 
                Serialization.Serialize.Json(DaemonProcess.DaemonStatistics) : 
                DaemonProcess.DaemonStatistics.ToString()
               );

        void ICommand.ClearData() { return; }
    }
}
