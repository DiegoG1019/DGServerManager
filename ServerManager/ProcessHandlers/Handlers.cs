using DiegoG.CLI;
using DiegoG.Utilities.Collections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Daemon.ProcessHandlers
{
    public static class Handlers
    {
        private static Dictionary<string, Type> LoadedHandlerTypes { get; } = new();
        public static ProcessHandler NewHandler(string name, Process process, CommandArguments args)
            => (ProcessHandler)Activator.CreateInstance(LoadedHandlerTypes[name], process, args);

        public static IEnumerable<string> AvailableHandlers { get; private set; }

        static Handlers() => LoadHandlers(Assembly.GetExecutingAssembly());

        internal static void LoadHandlers(Assembly asm)
        {
            try
            {
                List<string> ah = new();
                foreach (var (ty, att) in ReflectionCollectionMethods.GetAllTypesAndAttributeInstanceTupleFromAssembly(ProcHandlerAttribute.ThisType, false, new[] { asm }))
                {
                    var attrn = ((ProcHandlerAttribute)att[0]).HandlerName;
                    LoadedHandlerTypes.Add(attrn, ty);
                    if (attrn == "special:LuaHandler")
                        continue;
                    ah.Add(attrn);
                }
                AvailableHandlers = ah;
            }
            catch (System.Exception e)
            {
                throw new TypeLoadException($"All classes attributed with ProcHandlerAttribute must not be generic, abstract, or static, must have a constructor with the signature of (Process, CommandArguments?), and must implement ProcessHandler directly or indirectly. ProcHandlerAttribute is not inheritable. Check inner exception for more details.", e);
            }
        }

    }
#nullable enable
    public abstract class ProcessHandler
    {
        public Process Process { get; init; }
        public CommandArguments? HandlerArguments { get; init; }
        public abstract Task Start();
        public abstract Task Handle();
        public abstract void End();

        protected ProcessHandler(Process proc, CommandArguments? args)
        {
            Process = proc;
            HandlerArguments = args;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ProcHandlerAttribute : Attribute
    {
        internal static readonly Type ThisType = typeof(ProcHandlerAttribute);
        public ProcHandlerAttribute(string handlerName) => HandlerName = handlerName;
        public string HandlerName { get; init; }
    }
}
