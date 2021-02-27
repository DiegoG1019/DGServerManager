using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DiegoG.Utilities.Settings;
using PropertyChanged;

namespace DiegoG.ServerManager
{
    [AddINotifyPropertyChangedInterface]
    public class InteractiveSettings : ISettings
    {
        public string SettingsType => "DGServerManager.Interactive.Settings";
        public ulong Version => 0;
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
