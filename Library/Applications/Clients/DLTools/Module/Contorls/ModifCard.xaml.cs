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

namespace DLTools.Module.Contorls
{
    /// <summary>
    /// ModifCard.xaml 的交互逻辑
    /// </summary>
    public partial class ModifCard : UserControl
    {
        // 为 Header 属性定义依赖属性
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                "Header",
                typeof(string),
                typeof(ModifCard),
                new PropertyMetadata("标题"));

        // 为 Description 属性定义依赖属性
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(
                "Description",
                typeof(string),
                typeof(ModifCard),
                new PropertyMetadata("描述"));

        // 为 Status 属性定义依赖属性
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(
                "Status",
                typeof(bool),
                typeof(ModifCard),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        // 定义 StatusChanged 事件
        public static readonly RoutedEvent StatusChangedEvent =
            EventManager.RegisterRoutedEvent(
                "StatusChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(ModifCard));

        // Header 属性
        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        // Description 属性
        public string Description
        {
            get { return (string)GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        // Status 属性
        public bool Status
        {
            get { return (bool)GetValue(StatusProperty); }
            set { SetValue(StatusProperty, value); }
        }

        // StatusChanged 事件
        public event RoutedEventHandler StatusChanged
        {
            add { AddHandler(StatusChangedEvent, value); }
            remove { RemoveHandler(StatusChangedEvent, value); }
        }

        public ModifCard()
        {
            InitializeComponent();

            // 将UserControl作为DataContext，使绑定可以工作
            this.DataContext = this;
        }

        // ToggleSwitch 状态变化时的事件处理
        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            // 触发 StatusChanged 事件
            RoutedEventArgs args = new RoutedEventArgs(StatusChangedEvent, this);
            RaiseEvent(args);
        }
    }
}
