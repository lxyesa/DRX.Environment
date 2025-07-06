using Drx.Sdk.Text;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Text.Json;

namespace DLTools.Scripts
{
    public class GlobalSettings
    {
        private static readonly Lazy<GlobalSettings> _instance = new Lazy<GlobalSettings>(() => new GlobalSettings());

        public static GlobalSettings Instance => _instance.Value;

        public ElementTheme AppTheme { get; set; }
        public bool AppDirverHotKey { get; set; }   // 是否启用驱动级热键
        public bool AppHotKeyListener { get; set; } // 是否启用热键监听器
        public bool AppListenerProcess { get; set; } // 是否启用进程监听器
        public string GamePath { get; set; } = string.Empty;

        public void Save()
        {
            JsonFile.WriteToFile(this, "setting.json");
        }

        public GlobalSettings Load()
        {
            try
            {
                var cache = JsonFile.ReadFromFile<GlobalSettings>("setting.json");
                AppTheme = cache!.AppTheme;
                AppDirverHotKey = cache.AppDirverHotKey;
                GamePath = cache.GamePath;
                AppHotKeyListener = cache.AppHotKeyListener;
                AppListenerProcess = cache.AppListenerProcess;

                

                return this;
            }
            catch
            {
                Console.WriteLine("设置载入失败：使用默认设置");

                return new GlobalSettings();
            }
        }
    }
}
