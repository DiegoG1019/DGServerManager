using DiegoG.CLI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.DaemonCommands
{
    [CLICommand]
    public class Reload : ICommand
    {
        public string HelpExplanation => "Used to reload specific aspects of the Manager";
        public string HelpUsage => "reload [option]";
        public string Trigger => "reload";
        public string Alias => null;
        public IEnumerable<(string, string)> HelpOptions { get; } = new[]
        {
            ("settings","Reloads Daemon Settings from file"),
            ("all", "Reloads all available options")
        };
        public Task<string> Action(CommandArguments args)
        {
            switch (args.Arguments[0])
            {
                case "reload":
                    DaemonProcess.EnqueueSensitive(() => DaemonProcess.ApplySettings());
                    break;
                case "all":
                    DaemonProcess.EnqueueSensitive(() => DaemonProcess.ApplySettings());
                    break;
            }
            return Task.FromResult("Reload scheduled");
        }
        void ICommand.ClearData() { return; }
    }
}
