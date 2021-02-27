using DiegoG.CLI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.DaemonCommands
{
    [CLICommand]
    public class RetrieveBuffered : ICommand
    {
        private static readonly object SyncRoot = new();
        public const string Unfinished = "_unfinished";
        private static ConcurrentDictionary<string, string> ResultBuffer { get; } = new();
        public static bool AllocateResultBuffer(string buffer)
        {
            lock (SyncRoot)
                return ResultBuffer.TryAdd(buffer, Unfinished);
        }
        public static void SetResultBuffer(string buffer, string result)
        {
            lock (SyncRoot)
                ResultBuffer[buffer] = result;
        }
        public string HelpExplanation { get; } = "A command to retrieve a previously buffered response";
        public string HelpUsage { get; } = "retrieve (buffername)";
        public string Trigger => "retrieve";
        public string Alias => null;
        public IEnumerable<(string Option, string Explanation)> HelpOptions { get; } = new[]
        {
            ("buffername", "The name of a previously buffered message result")
        };
        public Task<string> Action(CommandArguments args)
        {
            lock (SyncRoot)
                return Task.FromResult(!ResultBuffer.TryRemove(args.Arguments[1], out string result) ? Unfinished : result);
        }
        void ICommand.ClearData() => ResultBuffer.Clear();
    }
}
