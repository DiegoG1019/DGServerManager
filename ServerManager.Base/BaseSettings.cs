using DiegoG.Utilities.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyChanged;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace DiegoG.ServerManager.Interprocess
{
    [AddINotifyPropertyChangedInterface]
    public class BaseSettings : ApplicationSettings
    {
        public override string SettingsType => "DGServerManager.Base.Settings";
        public override ulong Version => base.Version + 0;
        [IgnoreDataMember, JsonIgnore]
        public override bool Console => false;
    }
}
