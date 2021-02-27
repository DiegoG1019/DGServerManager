using DiegoG.Utilities.Collections;
using DiegoG.Utilities.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace DiegoG.ServerManager.Interprocess
{
    [SupportedOSPlatform("Linux")]
    public static class Directories
    {
        private static bool isinit;
        public static string Temp { get; private set; } = Path.GetTempPath();

        public static string Working { get; private set; } = Path.GetFullPath(Directory.GetCurrentDirectory());
        public static string InWorking(string n) => Path.Combine(Working, n);

        public static string Extensions { get; private set; }
        public static string InExtensions(string n) => Path.Combine(Extensions, n);

        public static string Settings { get; private set; } = Path.Combine("$HOME", ".config", "DGServerManager");
        public static string InSettings(params string[] n) => Path.Combine(Settings, Path.Combine(n));

        public static IEnumerable<(string Directory, string Path)> AllDirectories
            => isinit ? ReflectionCollectionMethods.GetAllMatchingTypeStaticPropertyNameValueTuple<string>(typeof(Directories)) : throw new InvalidOperationException("Directories has not been initialized");

        public static void InitApplicationDirectories()
        {
            Directory.CreateDirectory(Settings);
            isinit = true;
        }
        public static void InitOtherDirectories()
        {
            Extensions = Settings<DaemonSettings>.Current.ExtensionDir;
            Directory.CreateDirectory(Extensions);
        }
    }
}
