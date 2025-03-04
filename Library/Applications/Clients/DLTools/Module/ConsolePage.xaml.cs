using Drx.Sdk.Ui.Wpf.Console;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DLTools.Module
{
    /// <summary>
    /// ConsolePage.xaml 的交互逻辑
    /// </summary>
    public partial class ConsolePage : Page
    {
        public ConsolePage()
        {
            InitializeComponent();

            Console.WriteLine("控制台版本 v1.0.0");
            Console.WriteLine("该调试工具仅对DRX SDK进行开放，不对外开放");
            Console.WriteLine();
            Console.WriteLine("[Debug] 鱼儿的控制台正在初始化...");
        }
    }
}
