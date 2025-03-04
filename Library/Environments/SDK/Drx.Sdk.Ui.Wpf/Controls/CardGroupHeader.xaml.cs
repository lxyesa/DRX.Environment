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

namespace Drx.Sdk.Ui.Wpf.Controls
{
    /// <summary>
    /// CardGroupHeader.xaml 的交互逻辑
    /// </summary>
    public partial class CardGroupHeader : UserControl
    {
        /// <summary>
        /// HeaderIcon 属性的依赖属性，支持FontIcon作为参数
        /// </summary>
        public static readonly DependencyProperty HeaderIconProperty =
        DependencyProperty.Register(
        "HeaderIcon",
        typeof(object),
        typeof(CardGroupHeader),
        new PropertyMetadata(null, OnHeaderIconChanged));

        public static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(
        "HeaderContent",
        typeof(object),
        typeof(CardGroupHeader),
        new PropertyMetadata(null));

        /// <summary>
        /// 获取或设置头部图标，支持FontIcon作为参数
        /// </summary>
        public object HeaderIcon
        {
            get { return GetValue(HeaderIconProperty); }
            set { SetValue(HeaderIconProperty, value); }
        }

        public object HeaderContent
        {
            get { return GetValue(HeaderContentProperty); }
            set { SetValue(HeaderContentProperty, value); }
        }

        /// <summary>
        /// HeaderIcon 属性更改时的回调方法
        /// </summary>
        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardGroupHeader header)
            {
                // 如果 IconPresenter 存在
                if (header.IconPresenter != null)
                {
                    // 如果 HeaderIcon 为 null，则隐藏 IconPresenter
                    header.IconPresenter.Visibility = e.NewValue == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                }
            }
        }

        public CardGroupHeader()
        {
            InitializeComponent();

            // 初始化时设置 IconPresenter 的可见性
            if (IconPresenter != null)
            {
                IconPresenter.Visibility = HeaderIcon == null
                ? Visibility.Collapsed
                : Visibility.Visible;
            }
        }
    }
}
