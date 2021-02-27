using DiegoG.CLI;
using DiegoG.ServerManager.Interprocess;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Interactive.InteractiveCommands
{
    [CLICommand]
    class RequestBuffered : ICommand
    {
        public string HelpExplanation { get; } = "Sends a request to the daemon to schedule a task and buffer its result for later retrieval. Beware that any process can withdraw and overwrite this result, causing it to be deleted by the Daemon";
        public string HelpUsage { get; } = "request-buffered (buffername)";
        public string Trigger => "request-buffered";
        public string Alias => "reb";
        public IEnumerable<(string Option, string Explanation)> HelpOptions { get; } = new[]
        {
            ("buffername","The name of the buffer where the Daemon should store the requested result, and where to retrieve it from")
        };
        public Task<string> Action(CommandArguments args) => InterprocessStream.
        void ICommand.ClearData() { return; }
    }
}
