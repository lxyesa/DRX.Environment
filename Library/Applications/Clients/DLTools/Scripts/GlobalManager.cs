using System.Windows;
using System.Windows.Controls;

namespace DLTools.Scripts
{
    internal class GlobalManager
    {
        private static GlobalManager? _instance;
        private static readonly Lock _lock = new();

        // 单例访问属性
        public static GlobalManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (_lock)
                {
                    _instance ??= new GlobalManager();
                }
                return _instance;
            }
        }

        // =========================================== 全局属性
        public Window? mainWindow { get; set; }
        public Dictionary<string, Page?> pages { get; set; } = new Dictionary<string, Page?>();

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

        public Page? GetPage<T>(string key) where T : Page, new()
        {
            if (pages.TryGetValue(key, out var page))
            {
                return page;
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
            if (!pages.TryGetValue(key, out var currentPage)) return;
            // 获取key对应的页面，然后获取其类型，然后实例化一个新的同类型页面，然后替换原有页面
            var pageType = currentPage?.GetType();

            // 创建新实例
            var newPage = (Page)Activator.CreateInstance(pageType)!;

            // 替换原有页面
            pages[key] = newPage;
        }
    }
}
