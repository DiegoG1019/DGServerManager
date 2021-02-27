using DiegoG.CLI;
using DiegoG.Utilities;
using DiegoG.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DiegoG.ServerManager.Daemon.DaemonProcess;

namespace DiegoG.ServerManager.Daemon.DaemonCommands
{
    [CLICommand]
    public class AttachProcess : ICommand
    {
        public string HelpExplanation => "Starts and Attaches a new Process for the Daemon to watch.\n If you wish to attach an existing process, use --existing with the respective pid\nHandler name is string leading to a LuaScript file or preced it by a '__' to use a default handler";
        public string HelpUsage => "attach-process (path-to-file [--existing=pid]) handler-name([handlerargs]) ...processargs ";
        public string Trigger => "attach-process";
        public string Alias => "ac";
        public IEnumerable<(string, string)> HelpOptions { get; } = new[]
        {
            ("--existing=pid","Replaces path-to-file and instructs the process watcher to add an existing process to the watchlist"),
            ("handler-name", $"The path leading to the Lua script file, you may also preceed input a string in the form of `container:handlername` to use a C# handler"),
            ("processargs","The rest of the arguments are to be passed to the process"),
            ("handlerargs","Right after handler-name, without spacing, add a '()' (function call notation) and place the arguments you may wish to pass. If you don't wish to pass any, don't use the notation. Every handler must NOT REQUIRE these arguments. Refer to the extension's documentation. Arguments within the '()' are parsed like any other arguments, no need for commas."),
            ("Lua handlers", "A script containing a function called 'Handle' as a Lua function, it's not mandatory for it to accept arguments, but it will otherwise be unable to do anything. They will also be fitted with extra context from the application. Refer to the ReadMe for more info"),
            ("Available Handlers:", ProcessHandlers.Handlers.AvailableHandlers.Flatten())
        };

        public Task<string> Action(CommandArguments clargs)
        {
            var arguments = clargs.Arguments.ToArray();
            Process newproc = null;
            if (clargs.Options.Contains("existing"))
                newproc = Process.GetProcessById(int.Parse(clargs.Options.First(s => s.StartsWith("existing")).Split("=")[1]));

            var procargs = arguments.StartingAtIndex(3);

            if (newproc is null)
                newproc = Process.Start(arguments[1], procargs.Flatten());

            var handlerargsstr = clargs.Original.Flatten("", true).MatchSubstring('(', ')', true);
            var handlerargs = Commands.FullSplit(handlerargsstr);

            if (arguments[2].CountMatches(':') == 1)
            {
                DaemonProcess.AttachChildProcess(newproc, arguments[2], handlerargs);
                return Task.FromResult("Succesfully attached child process with a handler");
            }

            var lua = new LuaScript();
            lua.Initialize(arguments[2]);
            lua.Init();

            DaemonProcess.AttachChildProcess(newproc, "special:LuaHandler", handlerargs);

            return Task.FromResult("Succesfully attached child process with a Lua handler");
        }

        void ICommand.ClearData() { return; }
    }
}
