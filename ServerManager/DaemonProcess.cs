using System;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using DiegoG.CLI;
using DiegoG.ServerManager.Interprocess;
using DiegoG.Utilities;
using System.Linq;
using DiegoG.Utilities.Settings;
using System.Collections.Concurrent;
using System.Diagnostics;
using DiegoG.Utilities.Basic;
using DiegoG.Utilities.IO;
using System.Collections.Generic;
using DiegoG.ServerManager.Daemon.ProcessHandlers;
using DiegoG.Utilities.Collections;

namespace DiegoG.ServerManager.Daemon
{
    public static class DaemonProcess
    {
        private static ConcurrentQueue<Action<CommandArguments>> ActionQueue { get; } = new();
        private static ConcurrentQueue<Action> SensitiveActionQueue { get; } = new();
        private static ConcurrentQueue<Func<CommandArguments, Task>> AsyncActionQueue { get; } = new();
        internal static Process ThisProcess { get; private set; }
        internal static LinkedList<Func<CommandArguments, Task>> TaskList { get; } = new();
        internal static ConcurrentDictionary<int, ProcessHandler> ChildProcesses { get; } = new();
        public static StatisticsReport DaemonStatistics { get; private set; }
        public static TimeSpan Throttle { get; set; }
        public static CommandArguments Args { get; set; }
        
        public static void SetArgs(string[] args)
        {
            if (Args is not null)
                throw new InvalidOperationException("Daemon Args already set");
            Args = Commands.FullSplit(args);
        }
        public static void Invoke(Func<CommandArguments, Task> action) => AsyncActionQueue.Enqueue(action);
        public static void Invoke(Action<CommandArguments> action) => ActionQueue.Enqueue(action);
        /// <summary>
        /// Enqueue a Sensitive Action to be taken by the daemon, like reloading settings. These will be done right before the loop jumps back to the top
        /// </summary>
        public static void EnqueueSensitive(Action action) => SensitiveActionQueue.Enqueue(action);
        public static LinkedListNode<Func<CommandArguments, Task>> RegisterTask(Action<CommandArguments> action)
            => RegisterTask(c => Task.Run(() => action(c)));
        public static LinkedListNode<Func<CommandArguments, Task>> RegisterTask(Func<CommandArguments, Task> action)
        {
            if (DaemonStatistics.HighestTaskCount < TaskList.Count + 1)
                DaemonStatistics.HighestTaskCount = TaskList.Count + 1;
            return TaskList.AddLast(action);
        }

        public static void RemoveTask(LinkedListNode<Func<CommandArguments, Task>> node)
            => TaskList.Remove(node);
        public static void AttachChildProcess(Process process, string name, CommandArguments args = null)
        {
            if (!ChildProcesses.TryAdd(process.Id, Handlers.NewHandler(name, process, args)))
                throw new InvalidOperationException("Could not attach child process. Maybe it's already registered?");
            process.Exited += ChildProcess_Exited;
            DaemonStatistics.TotalChildProcessesAttached++;
        }

        public static bool GetChildProcess(int id, out ProcessHandler process)
            => ChildProcesses.TryGetValue(id, out process);

        public static bool KillChild(int id, out ProcessHandler process)
        {
            bool r = ChildProcesses.TryRemove(id, out process);
            if (!r)
                return r;
            if (!process.Process.HasExited)
                process.Process.Kill();
            process.Process.Exited -= ChildProcess_Exited;
            return r;
        }

        private static void ChildProcess_Exited(object sender, EventArgs e)
        {
            var proc = (Process)sender;
            KillChild(proc.Id, out var process);
            process.End();
        }

        public static async Task Run(CancellationToken cancellation)
        {
            AsyncTaskManager tasks = new();
            Log.Information("Starting Daemon");
            ThisProcess = Process.GetCurrentProcess();
            if (ThisProcess.StartInfo.UserName != "root")
                throw new InvalidOperationException("Requires root access");

            Log.Information("Processing CommandLine Arguments");
            await Commands.Call(Args);

            Settings<DaemonSettings>.SettingsChanged += SettingsChanged;
            Log.Information("Applying Settings");
            ApplySettings();

            DaemonStatistics = new() { StartTime = DateTime.Now, StartUser = ThisProcess.StartInfo.UserName };

            Log.Information("Entering Main Loop");
            while (!cancellation.IsCancellationRequested)
            {
                var throttletask = Task.Delay(Throttle, CancellationToken.None);
                
                foreach(var process in ChildProcesses.Values)
                    tasks.Add(process.Handle());

                foreach (var asyncaction in AsyncActionQueue)
                    tasks.Add(asyncaction(Args));

                var mcount = InterprocessStream.Daemon.Messages.Count;
                DaemonStatistics.AMessagesPPL.AddSample(mcount);
                DaemonStatistics.MessagesReceived += (ulong)mcount;
                foreach(var message in InterprocessStream.Daemon.Messages)
                {
                    Log.Information($"Received a {Enum.GetName(message.Type) ?? "unknown type"} message from {message.Sender}");
                    Log.Debug($"Message: {message}");
                    switch (message.Type)
                    {
                        case InterprocessStream.Message.MessageType.Regular:
                            tasks.Add(Commands.Call(message.Content));
                            break;
                        case InterprocessStream.Message.MessageType.Immediate:
                            await Commands.Call(message.Content);
                            break;
                        case InterprocessStream.Message.MessageType.Request:
                            InterprocessStream.Daemon.WriteResponse(new[] { await Commands.Call(message.Content) });
                            DaemonStatistics.RequestsProcessed++;
                            break;
                        case InterprocessStream.Message.MessageType.RequestCommand:
                            InterprocessStream.Daemon.WriteResponse(Commands.SplitCommandLine(await Commands.Call(message.Content)).ToArray());
                            DaemonStatistics.RequestsProcessed++;
                            break;
                        case InterprocessStream.Message.MessageType.BufferedRequest:
                            var arbr = DaemonCommands.RetrieveBuffered.AllocateResultBuffer(message.Content[0]);
                            if(arbr)
                                Invoke(async a => DaemonCommands.RetrieveBuffered.SetResultBuffer(message.Content[0], await Commands.Call(message.Content.StartingAtIndex(1).ToArray())));
                            InterprocessStream.Daemon.WriteResponse(new[] { arbr.ToString() });
                            DaemonStatistics.RequestsProcessed++;
                            break;
                        case InterprocessStream.Message.MessageType.Response:
                            Log.Error($"Unexpected MessageType: {message}");
                            break;
                        default:
                            Log.Error($"Unsupported/Unknown MessageType: {message}");
                            break;
                    }
                }

                DaemonStatistics.AActionsPL.AddSample(ActionQueue.Count);
                DaemonStatistics.TotalActionsExecuted += (ulong)ActionQueue.Count;
                foreach (var action in ActionQueue)
                    action(Args);

                //--
                DaemonStatistics.TotalTasksAwaited += (ulong)tasks.Count;
                DaemonStatistics.ATasksPL.AddSample(tasks.Count);

                await tasks;
                tasks.Clear();

                if (throttletask.IsCompleted)
                    DaemonStatistics.OverworkedLoops++;

                foreach (var action in SensitiveActionQueue)
                    action();
            }
            Log.Information("Termination Signal Received, Stopping Daemon");
        }

        internal static void ApplySettings()
        {
            DaemonSettings settings = Settings<DaemonSettings>.Current;
            Throttle = settings.SysThrottle;
            Log.Debug("Settings:");
            foreach (var p in Settings<DaemonSettings>.CurrentProperties)
                Log.Debug($"Setting \"{p.ObjectA}\" = {p.ObjectB}");
        }
        private static void SettingsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Log.Information("Settings Changed, Reapplying Settings");
            ApplySettings();
        }

        public sealed record StatusReport
        {
            public long MemoryUsage => ThisProcess.PrivateMemorySize64;
            public int AsyncTaskQueue => AsyncActionQueue.Count;
            public int TaskQueue => ActionQueue.Count;
            public int MessageQueue => InterprocessStream.Daemon.Messages.Count;

            public override string ToString()
                => Serialization.Serialize.Json(this);

            internal StatusReport() { }
        }

        public sealed record StatisticsReport
        {
            public DateTime StartTime { get; internal init; }
            public string StartUser { get; internal init; }

            public TimeSpan UpTime => DateTime.Now - StartTime;

            public ulong MessagesReceived { get; internal set; }
            public ulong RequestsProcessed { get; internal set; }
            public ulong TotalActionsExecuted { get; internal set; }
            public ulong TotalTasksAwaited { get; internal set; }
            public ulong TotalChildProcessesAttached { get; internal set; }
            public ulong OverworkedLoops { get; internal set; }
            public int HighestTaskCount { get; internal set; }
        
            public int WatchedProcesses => DaemonProcess.ChildProcesses.Count;
            public int RegisteredTasks => DaemonProcess.TaskList.Count;

            public double AverageMessagesProcessedPerLoop => AMessagesPPL.Average;
            public double AverageTasksPerLoop => ATasksPL.Average;
            public double AverageActionsPerLoop => AActionsPL.Average;

            internal AverageList AActionsPL { get; } = new(20);
            internal AverageList ATasksPL { get; } = new(20);
            internal AverageList AMessagesPPL { get; } = new(20);

            public override string ToString()
                => Serialization.Serialize.Json(this);

            internal StatisticsReport() { }
        }
    }
}
