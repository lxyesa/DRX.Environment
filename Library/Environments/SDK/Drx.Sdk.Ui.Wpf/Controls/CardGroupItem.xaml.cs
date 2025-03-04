using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Drx.Sdk.Ui.Wpf.Controls
{
    /// <summary>
    /// CardGroupItem.xaml 的交互逻辑
    /// </summary>
    public partial class CardGroupItem : UserControl
    {
        /// <summary>
        /// Content 属性的依赖属性
        /// </summary>
        public static new readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
        "Content",
        typeof(object),
        typeof(CardGroupItem),
        new PropertyMetadata(null));

        /// <summary>
        /// Background 属性的依赖属性
        /// </summary>
        public static readonly DependencyProperty CardBackgroundProperty =
        DependencyProperty.Register(
        "CardBackground",
        typeof(Brush),
        typeof(CardGroupItem),
        new PropertyMetadata(null));

        /// <summary>
        /// BorderBrush 属性的依赖属性
        /// </summary>
        public static readonly DependencyProperty CardBorderBrushProperty =
        DependencyProperty.Register(
        "CardBorderBrush",
        typeof(Brush),
        typeof(CardGroupItem),
        new PropertyMetadata(null));

        /// <summary>
        /// CornerRadius 属性的依赖属性
        /// </summary>
        public static readonly DependencyProperty CardCornerRadiusProperty =
        DependencyProperty.Register(
        "CardCornerRadius",
        typeof(CornerRadius),
        typeof(CardGroupItem),
        new PropertyMetadata(new CornerRadius(0)));

        /// <summary>
        /// 获取或设置卡片内容
        /// </summary>
        public new object Content
        {
            get { return GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        /// <summary>
        /// 获取或设置卡片背景
        /// </summary>
        public Brush CardBackground
        {
            get { return (Brush)GetValue(CardBackgroundProperty); }
            set { SetValue(CardBackgroundProperty, value); }
        }

        /// <summary>
        /// 获取或设置卡片边框颜色
        /// </summary>
        public Brush CardBorderBrush
        {
            get { return (Brush)GetValue(CardBorderBrushProperty); }
            set { SetValue(CardBorderBrushProperty, value); }
        }

        /// <summary>
        /// 获取或设置卡片圆角半径
        /// </summary>
        public CornerRadius CardCornerRadius
        {
            get { return (CornerRadius)GetValue(CardCornerRadiusProperty); }
            set { SetValue(CardCornerRadiusProperty, value); }
        }

        /// <summary>
        /// Clickable 属性的依赖属性
        /// </summary>
        public static readonly DependencyProperty ClickableProperty =
        DependencyProperty.Register(
        "Clickable",
        typeof(bool),
        typeof(CardGroupItem),
        new PropertyMetadata(false, OnClickablePropertyChanged));

        /// <summary>
        /// 获取或设置卡片是否可点击
        /// </summary>
        public bool Clickable
        {
            get { return (bool)GetValue(ClickableProperty); }
            set { SetValue(ClickableProperty, value); }
        }

        /// <summary>
        /// HeaderIcon 属性的依赖属性，支持FontIcon作为参数
        /// </summary>
        public static readonly DependencyProperty HeaderIconProperty =
        DependencyProperty.Register(
        "HeaderIcon",
        typeof(object),
        typeof(CardGroupItem),
        new PropertyMetadata(null, OnHeaderIconChanged));

        /// <summary>
        /// 获取或设置头部图标，支持FontIcon作为参数
        /// </summary>
        public object HeaderIcon
        {
            get { return GetValue(HeaderIconProperty); }
            set { SetValue(HeaderIconProperty, value); }
        }

        /// <summary>
        /// 定义点击事件的委托
        /// </summary>
        public event RoutedEventHandler Click;

        /// <summary>
        /// 触发点击事件的方法
        /// </summary>
        protected virtual void OnClick()
        {
            // 如果卡片可点击且已注册点击事件处理器，则触发事件
            if (Clickable && Click != null)
            {
                Click(this, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// Clickable 属性更改时的回调
        /// </summary>
        private static void OnClickablePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardGroupItem cardItem)
            {
                bool isClickable = (bool)e.NewValue;

                // 更新 ChevronR 的可见性
                cardItem.ChevronR.Visibility = isClickable ? Visibility.Visible : Visibility.Collapsed;

                // 更新 Border 的鼠标样式
                cardItem.border.Cursor = isClickable ? Cursors.Hand : Cursors.Arrow;

                VisualStateManager.GoToState(cardItem, isClickable ? "Clickable" : "NotClickable", true);
            }
        }

        /// <summary>
        /// HeaderIcon 属性更改时的回调方法
        /// </summary>
        private static void OnHeaderIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardGroupItem item)
            {
                // 如果 IconPresenter 存在
                if (item.IconPresenter != null)
                {
                    // 如果 HeaderIcon 为 null，则隐藏 IconPresenter
                    item.IconPresenter.Visibility = e.NewValue == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// Header 属性的依赖属性
        /// </summary>
        public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(
        "Header",
        typeof(string),
        typeof(CardGroupItem),
        new PropertyMetadata(null));

        /// <summary>
        /// 获取或设置头部文本
        /// </summary>
        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        /// <summary>
        /// Description 属性的依赖属性，支持 FrameworkElement 作为参数
        /// </summary>
        public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
        "Description",
        typeof(object),
        typeof(CardGroupItem),
        new PropertyMetadata(null, OnDescriptionChanged));

        public static readonly DependencyProperty ItemContentProperty =
            DependencyProperty.Register(
                "ItemContent",
                typeof(object),
                typeof(CardGroupItem),
                new PropertyMetadata(null));

        public object ItemContent
        {
            get { return GetValue(ItemContentProperty); }
            set { SetValue(ItemContentProperty, value); }
        }

        /// <summary>
        /// 获取或设置描述内容，支持 FrameworkElement 作为参数
        /// </summary>
        public object Description
        {
            get { return GetValue(DescriptionProperty); }
            set { SetValue(DescriptionProperty, value); }
        }

        /// <summary>
        /// Description 属性更改时的回调方法
        /// </summary>
        private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CardGroupItem item)
            {
                // 如果 DescriptionPresenter 存在
                if (item.DescriptionPresenter != null)
                {
                    // 如果 Description 为 null，则隐藏 DescriptionPresenter
                    item.DescriptionPresenter.Visibility = e.NewValue == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                }
            }
        }

        public CardGroupItem()
        {
            InitializeComponent();

            // 初始化时设置 IconPresenter 的可见性
            if (IconPresenter != null)
            {
                IconPresenter.Visibility = HeaderIcon == null
                ? Visibility.Collapsed
                : Visibility.Visible;
            }

            // 初始化时设置 DescriptionPresenter 的可见性
            if (DescriptionPresenter != null)
            {
                DescriptionPresenter.Visibility = Description == null
                ? Visibility.Collapsed
                : Visibility.Visible;
            }

            // 添加鼠标事件处理
            border.MouseEnter += (s, e) =>
                {
                    if (Clickable)
                    {
                        VisualStateManager.GoToState(this, "MouseOver", true);
                        border.Background = (Brush)FindResource("ControlFillColorSecondaryBrush");
                        border.BorderBrush = (Brush)FindResource("ControlElevationBorderBrush");
                    }
                };

            border.MouseLeave += (s, e) =>
                    {
                        if (Clickable)
                        {
                            VisualStateManager.GoToState(this, "Normal", true);
                            border.Background = (Brush)FindResource("ControlFillColorDefaultBrush");
                            border.BorderBrush = (Brush)FindResource("CardStrokeColorDefaultBrush");
                        }
                    };

            // 添加鼠标点击事件处理
            border.MouseLeftButtonUp += (s, e) =>
                        {
                            if (Clickable)
                            {
                                OnClick();
                                e.Handled = true;
                            }
                        };
        }
    }
}
