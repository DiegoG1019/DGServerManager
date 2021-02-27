using DiegoG.CLI;
using NLua;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon
{
    public class LuaScript
    {
        private Lua Lua { get; init; }
        private LuaTable LuaContext { get; init; }
        private LuaTable AsyncResults { get; init; }
        private LinkedList<string> OccupiedAsyncIds { get; } = new();
        private Dictionary<LuaFunction, LinkedListNode<Func<CommandArguments, Task>>> RegisteredLuaFunctions { get; } = new();
        private LuaFunction InitFunc { get; init; }
        private LuaFunction HandleFunc { get; init; }
        private LuaFunction EndFunc { get; init; }
        private LuaFunction StartFunc { get; init; }

        public static StatisticsReport DaemonStatistics => DaemonProcess.DaemonStatistics;

        public void Init() => InitFunc.Call();
        public void Handle(Process process, CommandArguments handleargs) => HandleFunc.Call(process, handleargs);
        public void End(Process process, CommandArguments handleargs) => EndFunc.Call(process, handleargs);
        public void Start(Process process, CommandArguments handleargs) => StartFunc.Call(process, handleargs);
        public void EnqueueCommandCall(params string[] args) => DaemonProcess.Invoke(d => Commands.Call(args));
        public string Command(params string[] args) => Commands.Call(args).Result;
        public void RegisterTask(object o)
        {
            var func = (LuaFunction)o;
            RegisteredLuaFunctions[func] = DaemonProcess.RegisterTask(c => func.Call(c));
        }

        public void UnregisterTask(object o)
        {
            var func = (LuaFunction)o;
            DaemonProcess.RemoveTask(RegisteredLuaFunctions[func]);
            RegisteredLuaFunctions.Remove(func);
        }
        public void CommandAsync(string id, params string[] args)
        {
            if (OccupiedAsyncIds.Contains(id))
                throw new InvalidOperationException("This id is already occupied");
            OccupiedAsyncIds.AddLast(id);
            DaemonProcess.Invoke(async d =>
            {
                AsyncResults[id] = await Commands.Call(args);
                OccupiedAsyncIds.Remove(id);
            });
        }
        private static readonly string[] LuaDisabled = new[]
        { "os.exit", "os.execute", "os.remove", "os.rename", "io.popen",};
        internal LuaScript()
        {
            const string _asyncresults = "Context:AsyncResults";
            const string _messages = "Context.MessageBoard";

            Lua = new NLua.Lua();

            Lua.LoadCLRPackage();
            Lua["Daemon"] = this;
            Lua.DoString("import('System')");
            foreach (var s in LuaDisabled)
                Lua[s] = null;

            LuaContext = Lua.GetTable("_G");
            Lua.NewTable(_asyncresults);
            AsyncResults = Lua.GetTable(_asyncresults);
            Lua.NewTable(_messages);
            var msges = Lua.GetTable(_messages);
            msges["subscribers"] = MessageBoard.Subscribers;
            msges["boards"] = MessageBoard.Boards;
        }

        public void Initialize(string scriptfile)
        {
            Lua.DoFile(scriptfile);
            if (Lua.GetFunction("handle") is null || Lua.GetFunction("init") is null || Lua.GetFunction("end") is null || Lua.GetFunction("start") is null)
                throw new Exception.InvalidScriptException("A valid script must contain 'handle', 'init', 'start' and 'end' global functions");
        }
    }
}
