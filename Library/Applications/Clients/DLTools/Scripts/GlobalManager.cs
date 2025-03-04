using DLTools.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DLTools.Scripts
{
    internal class GlobalManager
    {
        private static GlobalManager _instance;
        private static readonly object _lock = new object();

        // 私有构造函数，防止外部直接实例化
        private GlobalManager()
        {
        }

        // 单例访问属性
        public static GlobalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GlobalManager();
                        }
                    }
                }
                return _instance;
            }
        }

        // =========================================== 全局属性
        public Window mainWindow { get; set; }
        public Dictionary<string, Page> pages { get; set; } = new Dictionary<string, Page>();

        // 初始化方法
        public void Initialize()
        {

        }

        public void AddPage<T>(string key) where T : Page, new()
        {
            if (!pages.ContainsKey(key))
            {
                pages.Add(key, new T());
            }
        }

        public Page GetPage<T>(string key) where T : Page, new()
        {
            if (pages.ContainsKey(key))
            {
                return pages[key];
            }
            else
            {
                // 如果key不存在则实例化一个新的Key，并执行AddPage方法，然后返回新的页面
                AddPage<T>(key);
                return pages[key];
            }
        }

        public void ResetPage(string key)
        {
            if (pages.ContainsKey(key))
            {
                // 获取key对应的页面，然后获取其类型，然后实例化一个新的同类型页面，然后替换原有页面
                Page currentPage = pages[key];
                Type pageType = currentPage.GetType();

                // 创建新实例
                Page newPage = (Page)Activator.CreateInstance(pageType);

                // 替换原有页面
                pages[key] = newPage!;
            }
        }
    }
}
