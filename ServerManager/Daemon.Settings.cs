using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using DiegoG.Utilities.Settings;
using PropertyChanged;
using System.IO;

namespace DiegoG.ServerManager
{
    [AddINotifyPropertyChangedInterface]
    public class DaemonSettings : ICommentedSettings
    {
        public string SettingsType => "DGServerManager.Daemon.Settings";
        public ulong Version => 0;
        public event PropertyChangedEventHandler PropertyChanged;
        public TimeSpan SysThrottle { get; set; } = TimeSpan.FromMilliseconds(80);
        public string ExtensionDir { get; set; } = Path.Combine("$HOME", "DGServerManager", "extensions");
        public string[] _Comments { get; } = Array.Empty<string>();
        public string[] _Usage { get; } = new[]
        {
            "ExtensionDir: Specifies the directory where the manager will look for exceptions",
            "Version and Type: These are used as parsing directives, changing these will result in this file being recognized as invalid.",
            "SysThrottle: Throttles the execution of the application. If a loop takes less time to complete than the specified Throttle, it will wait the remaining amount of time. Increasing this value can save CPU Time, but reduces performance significantly by deallocating that time from the application.",
        };
    }
}