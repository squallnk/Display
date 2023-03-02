﻿using Data;
using Display.Control;
using Display.Helper;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static Display.Control.CustomMediaPlayerElement;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ActorInfoPage : Page
    {
        //为返回动画做准备（需启动缓存）
        public VideoCoverDisplayClass _storeditem;


        public ActorInfoPage()
        {
            //启动缓存（为了返回无需过长等待，也为了返回动画）
            NavigationCacheMode = NavigationCacheMode.Enabled;

            this.InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            //准备动画
            //限定为 返回操作
            if (e.NavigationMode == NavigationMode.Back)
            {
                //指定页面
                if (e.SourcePageType == typeof(ActorsPage))
                {
                    var animation = ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackConnectedAnimation",videoControl.HeaderCover);

                    //返回动画应迅速
                    animation.Configuration = new DirectConnectedAnimationConfiguration();
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            //过渡动画
            if (e.NavigationMode == NavigationMode.Back)
            {
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackConnectedAnimation");
                if (animation != null)
                {
                    videoControl.StartAnimation(animation, _storeditem);
                }
            }
            else if (e.NavigationMode == NavigationMode.New)
            {
                var item = e.Parameter;
                if (item == null) return;
                //需要显示的是搜索结果
                if (item is Tuple<List<string>, string, bool> tuple)
                {
                    Tuple<List<string>, string> typesAndName = new (tuple.Item1, tuple.Item2);

                    LoadShowInfo(typesAndName, tuple.Item3);
                }
                else
                {
                    return;
                }

                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ForwardConnectedAnimation");
                if (animation != null)
                {
                    if(tuple.Item1.Count==1 && tuple.Item1.FirstOrDefault() == "actor")
                    {
                        animation.TryStart(videoControl.HeaderCover);
                    }
                    else
                    {
                        animation.TryStart(videoControl.showNameTextBlock);
                    }
                }
            }
        }

        private void LoadShowInfo(Tuple<List<string>, string> typesAndName, bool isFuzzyQueryActor)
        {
            videoControl.ReLoadSearchResult(typesAndName.Item1, typesAndName.Item2, isFuzzyQueryActor);
        }

        /// <summary>
        /// 选项选中后跳转至详情页
        /// </summary>
        private void OnClicked(object sender, RoutedEventArgs e)
        {
            VideoCoverDisplayClass item = (sender as Button).DataContext as VideoCoverDisplayClass;

            //准备动画
            videoControl.PrepareAnimation(item);
            _storeditem = item;
            Frame.Navigate(typeof(DetailInfoPage), item, new SuppressNavigationTransitionInfo());
        }

        /// <summary>
        /// 视频播放页面跳转
        /// </summary>
        /// <param Name="sender"></param>
        /// <param Name="e"></param>
        private async void VideoPlay_Click(object sender, RoutedEventArgs e)
        {
            var item = videoControl;

            var VideoPlayButton = (Button)sender;
            var videoInfo = VideoPlayButton.DataContext as VideoCoverDisplayClass;

            //播放失败列表（imgUrl就是pc）
            if(videoInfo.series == "fail")
            {
                await PlayeVideoHelper.PlayeVideo(videoInfo.imageurl, this.XamlRoot, playType:PlayType.fail);
                return;
            }

            List<Datum> videoInfoList = DataAccess.loadFileInfoByTruename(videoInfo.truename);

            _storeditem = videoInfo;

            //没有
            if (videoInfoList.Count == 0)
            {
                VideoPlayButton.Flyout = new Flyout()
                {
                    Content = new TextBlock() { Text = "经查询，本地数据库无该文件，请导入后继续" }
                };
            }
            else if (videoInfoList.Count == 1)
            {
                await PlayeVideoHelper.PlayeVideo(videoInfoList[0].pc, this.XamlRoot, trueName: videoInfo.truename, lastPage: this);
            }

            //有多集
            else
            {
                List<Datum> multisetList = new();
                foreach (var videoinfo in videoInfoList)
                {
                    multisetList.Add(videoinfo);
                }

                multisetList = multisetList.OrderBy(item => item.n).ToList();

                ContentsPage.DetailInfo.SelectSingleVideoToPlay newPage = new(multisetList, videoInfo.truename);
                newPage.ContentListView.ItemClick += ContentListView_ItemClick; ;

                VideoPlayButton.Flyout = new Flyout()
                {
                    Content = newPage
                };
            }
        }

        private async void ContentListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var SingleVideoInfo = e.ClickedItem as Data.Datum;

            if (sender is not ListView listView) return;
            if (listView.DataContext is not string trueName) return;

            await PlayeVideoHelper.PlayeVideo(SingleVideoInfo.pc, this.XamlRoot, trueName: trueName, lastPage: this);
        }

        private async void SingleVideoPlayClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Grid VideoPlayButton) return;

            if (VideoPlayButton.DataContext is not Datum datum) return;

            await PlayeVideoHelper.PlayeVideo(datum.pc, this.XamlRoot, playType: CustomMediaPlayerElement.PlayType.fail);
        }
    }

}
