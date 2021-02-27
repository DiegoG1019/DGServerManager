using DiegoG.Utilities.Settings;
using Serilog;
using System;
using DiegoG.ServerManager.Interprocess;
using System.Threading.Tasks;
using DiegoG.Utilities.Collections;
using DiegoG.CLI;
using System.Linq;
using static DiegoG.ServerManager.Interprocess.InterprocessStream;
using System.Threading;

namespace DiegoG.ServerManager.Interactive
{
    public static class InteractiveProcess
    {
        public static void W(string s) => Console.Write(s);
        public static void WL(string s) => Console.WriteLine(s);
        public static string RL() => Console.ReadLine();
        public static Task Run(string[] args, CancellationToken cancellation)
        {
            var newargs = new string[args.Length + 1];
            args.CopyTo(newargs, 1);
            newargs[0] = "_newinteractive";

            WL($"Starting Interactive DGServerManager CLI with args: {args.Flatten()}");
            Client.Write(newargs, true);

            while (!cancellation.IsCancellationRequested)
            {
                W("> ");
                WL(Client.WriteRequest(Commands.SplitCommandLine(RL()).ToArray()).ToString());
            }
            return Task.CompletedTask;
        }
    }
}
