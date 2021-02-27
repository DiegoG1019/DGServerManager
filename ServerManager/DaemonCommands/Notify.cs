using DiegoG.CLI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.DaemonCommands
{
    [CLICommand]
    public class Notify : ICommand
    {
        public string HelpExplanation { get; } = "Issues a notification through a specific buffer, for a given handler or task to read";
        public string HelpUsage { get; } = "notify (buffer) (args...)";
        public string Trigger => "notify";
        public string Alias => "nf";
        public IEnumerable<(string Option, string Explanation)> HelpOptions { get; } = new[]
        {
            ("buffer", "The buffer name to be used and to send the message to"),
            ("args...", "Every other argument will be sent to ")
        };
        public Task<string> Action(CommandArguments args) => throw new NotImplementedException();
        void ICommand.ClearData() { return; }
    }
}
