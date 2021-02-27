using DiegoG.CLI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.ProcessHandlers
{
    [ProcHandler("special:LuaHandler")]
    public class LuaHandler : ProcessHandler
    {
        public LuaScript Script { get; init; }
        public override void End() => Script.End(Process, HandlerArguments);
        public override Task Handle() => Task.Run(() => Script.Handle(Process, HandlerArguments));
        public override Task Start() => Task.Run(() => Script.Start(Process, HandlerArguments));
        public LuaHandler(Process process, CommandArguments args) : base(process, args) { }
    }
}
