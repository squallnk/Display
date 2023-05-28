﻿
using Display.Views;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Collections.ObjectModel;
using Display.Data;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.Controls
{
    public sealed partial class MultipleCoverShow : UserControl
    {
        public static readonly DependencyProperty MoreButtonVisibilityProperty =
            DependencyProperty.Register(nameof(MoreButtonVisibility), typeof(Visibility), typeof(ActorInfoPage), PropertyMetadata.Create(() => Visibility.Collapsed));
        
        public static readonly DependencyProperty RefreshButtonVisibilityProperty =
            DependencyProperty.Register(nameof(RefreshButtonVisibility), typeof(Visibility), typeof(ActorInfoPage), PropertyMetadata.Create(() => Visibility.Collapsed));

        //是否显示MoreButton
        public Visibility MoreButtonVisibility
        {
            get => (Visibility)GetValue(MoreButtonVisibilityProperty);
            set => SetValue(MoreButtonVisibilityProperty, value);
        }

        //是否显示RefreshButton
        public Visibility RefreshButtonVisibility
        {
            get => (Visibility)GetValue(RefreshButtonVisibilityProperty);
            set => SetValue(RefreshButtonVisibilityProperty, value);
        }

        public string ShowName { get; set; }

        public ObservableCollection<VideoCoverDisplayClass> CoverList { get; set; } = new();

        public MultipleCoverShow()
        {
            this.InitializeComponent();
        }

        //private void MultipleCoverShow_Loaded(object sender, RoutedEventArgs e)
        //{
        //    this.Loaded -= MultipleCoverShow_Loaded;

        //    ////调整大小并开始监听
        //    //tryUpdateCoverFlipItems();
        //    //NewAddGrid.SizeChanged += NewAddGrid_SizeChanged;

        //}


        //private void NewAddGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //    tryUpdateCoverFlipItems();
        //}

        ////CoverList不变的情况下，更新封面显示数量
        //private void tryUpdateCoverFlipItems()
        //{
        //    if (CoverList == null) return;

        //    //最大的显示数量
        //    int count = ((int)NewAddGrid.RenderSize.Width - 20 - 3 * showCount) / 300;
        //    //修正item间距
        //    count = ((int)NewAddGrid.RenderSize.Width - 20 - 3 * count) / 300;

        //    //什么时候更新封面
        //    // CoverList增减 / 最适宜显示数量发生改变
        //    if (_CoverCount != CoverList.Count || (count != showCount && count != 0))
        //    {
        //        showCount = count;


        //        UpdateCoverFlipItems(showCount, CoverList);
        //    }

        //    _CoverCount = CoverList.Count;
        //}

        //private  void UpdateCoverFlipItems(int count, List<VideoCoverDisplayClass> CoverList)
        //{
        //    NewAddFlipItems.Clear();

        //    for (int i = 0; i < CoverList.Count; i += count)
        //    {
        //        ObservableCollection<VideoCoverDisplayClass> NewItems = new();
        //        for (int j = 0; j < count && i + j < CoverList.Count; j++)
        //        {
        //            NewItems.Add(CoverList[i + j]);
        //        }
        //        NewAddFlipItems.Add(new CoverFlipItems() { CoverItems = NewItems });
        //    }
        //}


        //点击了图片
        public event ItemClickEventHandler ItemClick;
        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(sender, e);

        }

        private void Image_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }

        private void Image_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }

        private Visibility isContentNull(int coverCount)
        {
            return coverCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        }


        public event RoutedEventHandler MoreClick;
        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            MoreClick?.Invoke(sender, e);
        }

        public event RoutedEventHandler RefreshClick;
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshClick?.Invoke(sender,e);
        }
    }
}
