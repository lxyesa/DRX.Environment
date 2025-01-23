using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Interface
{
    public interface IPlugin
    {
        /// <summary>
        /// 加载插件
        /// </summary>
        /// <param name="loader">插件的加载器，一般来说，它必须为一个引擎实例</param>
        public void Load(IEngine loader);
        public void Unload();
        public bool IsLoaded { get; }
        public string Name { get; }
        public string Description { get; }
        public string Version { get; }
        public string Author { get; }
        public string AuthorUrl { get; }
        public string[] Dependencies { get; }
    }
}
