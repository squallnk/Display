﻿// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using Display.Data;
using Display.Helper;
using Display.Models;
using Display.WindowView;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Playback;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.ContentsPage.DatumList.VideoDisplay;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainPage : Page
{

    ObservableCollection<FilesInfo> PlayingVideoInfos = new();

    ObservableCollection<VideoInfo> CidInfos = new();

    ObservableCollection<FilesInfo> FilesInfos;

    IncrementalLoadDatumCollection FilesInfosCollection;

    private List<string> _highBitRateVideos = new();

    ObservableCollection<MetadataItem> _units;

    ListView LastFilesListView;

    WebApi webApi;

    private bool _isDisposing;


    /// <summary>
    /// 可播放的最大数量
    /// </summary>
    private int MaxCanPlayCount => (int)AppSettings.MaxVideoPlayCount;

    public MainPage(List<FilesInfo> filesInfos, ListView lastFilesListView)
    {
        this.InitializeComponent();

        this.LastFilesListView = lastFilesListView;

        FilesInfos = new ObservableCollection<FilesInfo>();
        filesInfos.ForEach(item => FilesInfos.Add(item));

        _units = new ObservableCollection<MetadataItem>()
            { new() { Label = "播放列表", Command = OpenFolderCommand, CommandParameter = 0 } };
        metadataControl.Items = _units;
        webApi = WebApi.GlobalWebApi;

        Unloaded += OnUnloaded;

        TryPlayVideoFromSelectedFiles(FilesInfos.ToList());
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Video_UniformGrid.PointerExited -= Video_UniformGrid_OnPointerExited;
        aTimer?.Stop();
    }

    private async void TryPlayVideoFromSelectedFiles(List<FilesInfo> filesInfos)
    {
        //var maxPlayCount = AppSettings.IsDefaultPlaySingleVideo ? 1 : MaxCanPlayCount;
        var maxPlayCount = MaxCanPlayCount;

        var videoList = filesInfos.Where(item => item.Type == FilesInfo.FileType.File && item.IsVideo)
            .Take(maxPlayCount).ToList();
        var videoCount = videoList.Count;

        if (maxPlayCount == 1)
        {
            tryPlayVideos(videoList);
        }
        else
        {
            if (videoCount >= maxPlayCount)
            {
                tryPlayVideos(videoList);
            }
            // count > 4
            else
            {
                var leftCount = maxPlayCount - videoCount;

                //是否有文件夹
                var folderList = filesInfos.Where(item => item.Type == FilesInfo.FileType.Folder).Take(leftCount)
                    .ToList();
                if (folderList.Count > 0)
                {

                    foreach (var folder in folderList)
                    {
                        var filesInfo = await webApi.GetFileAsync(folder.Cid, 40);

                        // 挑选视频数量
                        var videoInFolder = filesInfo.data
                            .Where(item => item.fid!=null && item.iv == 1).Take(leftCount).ToList();
                        var videoInFolderCount = videoInFolder.Count;

                        if (videoInFolderCount <= 0) continue;

                        foreach (var video in videoInFolder)
                        {
                            videoList.Add(new FilesInfo(video));
                            leftCount--;

                            if (leftCount == 0) break;
                        }

                        if (leftCount == 0) break;
                    }
                }

                tryPlayVideos(videoList);

            }
        }
    }

    private RelayCommand<long> _openFolderCommand;

    private RelayCommand<long> OpenFolderCommand =>
        _openFolderCommand ??= new RelayCommand<long>(OpenFolder);

    private async void OpenFolder(long cid)
    {
        var currentItem = _units.FirstOrDefault(item => (long)item.CommandParameter == cid);

        //不存在，返回
        if (currentItem.CommandParameter == null) return;

        //删除选中路径后面的路径
        var index = _units.IndexOf(currentItem);

        //不存在，返回
        if (index < 0) return;

        for (int i = _units.Count - 1; i > index; i--)
        {
            _units.RemoveAt(i);
        }

        //选中的是第一项
        if (index == 0)
        {
            VideoShow_ListView.ItemsSource = FilesInfos;
            return;
        }
        else
        {
            await FilesInfosCollection.SetCid(cid);
            VideoShow_ListView.ItemsSource = FilesInfosCollection;
        }

    }

    public void CreateWindow()
    {
        var window = new CommonWindow("播放");

        window.Closed += Window_Closed;
        window.Content = this;
        window.Activate();
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        _isDisposing = true;
        RemoveAllMediaControl();
    }

    private void RemoveAllMediaControl()
    {
        foreach (var child in Video_UniformGrid.Children)
        {
            //var videoControl = child as MediaPlayerElement;

            if (child is not MediaPlayerElement { Tag: MediaPlayerWithStreamSource oldMediaPlayerWithStreamSource }) continue;

            try
            {
                Debug.WriteLine("Dispose MediaPlayer");

                oldMediaPlayerWithStreamSource.Dispose();

                //videoControl?.MediaPlayer?.Dispose();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭MediaControl出错{ex.Message}");
            }
        }

        Video_UniformGrid.Children.Clear();
    }


    private void OpenFolder_Tapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is not Grid { DataContext: FilesInfo filesInfo }) return;


        ChangedFolder(filesInfo);
    }

    private void TextBlock_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not TextBlock { DataContext: FilesInfo filesInfo }) return;

        ChangedFolder(filesInfo);
    }

    private async void ChangedFolder(FilesInfo filesInfo)
    {
        //跳过文件
        if (filesInfo.Type == FilesInfo.FileType.File) return;

        if (FilesInfosCollection == null)
        {
            FilesInfosCollection = new IncrementalLoadDatumCollection(filesInfo.Cid);
        }
        else
        {
            await FilesInfosCollection.SetCid(filesInfo.Cid);
        }

        if (VideoShow_ListView.ItemsSource != FilesInfosCollection)
        {
            VideoShow_ListView.ItemsSource = FilesInfosCollection;
        }


        _units.Add(new MetadataItem
        {
            Label = filesInfo.Name,
            Command = OpenFolderCommand,
            CommandParameter = filesInfo.Cid,
        });
    }

    private void PlayVideoButton_Click(object sender, RoutedEventArgs e)
    {
        //检查选中的文件或文件夹
        if (VideoShow_ListView.SelectedItems.FirstOrDefault() is not FilesInfo) return;

        var filesInfo = VideoShow_ListView.SelectedItems.Cast<FilesInfo>().ToList();

        TryPlayVideoFromSelectedFiles(filesInfo);
    }

    private void ChangedVideo_UniformGrid(int videoCount)
    {
        switch (videoCount)
        {
            case 0:
                return;
            case 1:
                Video_UniformGrid.Rows = 1;
                Video_UniformGrid.Columns = 1;
                break;
            case 2:
                Video_UniformGrid.Rows = 2;
                Video_UniformGrid.Columns = 1;
                break;
            case > 2:
                Video_UniformGrid.Rows = 2;
                Video_UniformGrid.Columns = 2;
                break;
        }
    }

    private async Task<string> GetVideoUrl(FilesInfo file)
    {
        string videoUrl = null;
        var pickCode = file.PickCode;
        //转码成功，可以用m3u8
        if (file.Datum.vdi != 0)
        {
            var m3U8Infos = await webApi.GetM3U8InfoByPickCode(pickCode);

            if (m3U8Infos.Count > 0)
            {
                //选择最高分辨率的播放
                videoUrl = m3U8Infos[0].Url;
            }
        }
        // 视频未转码，尝试获取直链
        else
        {
            var downUrlList = await webApi.GetDownUrl(pickCode, GetInfoFromNetwork.DownUserAgent);

            if (downUrlList.Count > 0)
            {
                videoUrl = downUrlList.FirstOrDefault().Value;
            }
        }

        return videoUrl;

    }

    private MenuFlyout BuildMenuFlyout(FilesInfo file)
    {
        MenuFlyout menuFlyout = new();

        var menuFlyoutItemDeletedFromList = new MenuFlyoutItem()
        {
            Text = "从播放列表中移除",
            Icon = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE108" }
        };
        menuFlyoutItemDeletedFromList.Click += RemoveFileFromListButton_Click;
        menuFlyoutItemDeletedFromList.DataContext = file;

        var menuFlyoutItemDeletedFrom115 = new MenuFlyoutItem()
        {
            Text = "从115中删除",
            Icon = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uE107" }
        };
        menuFlyoutItemDeletedFrom115.Click += RemoveFileFrom115Button_Click;
        menuFlyoutItemDeletedFrom115.DataContext = file;


        var menuFlyoutItemLoadVideoAgain = new MenuFlyoutItem()
        {
            Text = "重新加载",
            Icon = new FontIcon { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\uF83E" }
        };
        menuFlyoutItemLoadVideoAgain.Click += LoadVideoAgainButton_Click;
        menuFlyoutItemLoadVideoAgain.DataContext = file;

        menuFlyout.Items.Add(menuFlyoutItemDeletedFromList);
        menuFlyout.Items.Add(menuFlyoutItemDeletedFrom115);
        menuFlyout.Items.Add(menuFlyoutItemLoadVideoAgain);

        return menuFlyout;
    }

    private int currentCount = 0;
    private async Task AddMediaElement(FilesInfo file, string videoUrl = null, int addIndex = -1)
    {
        currentCount++;
        Debug.WriteLine($"当前数量：{currentCount}");

        videoUrl ??= await GetVideoUrl(file);
        if (videoUrl == null) return;

        var menuFlyout = BuildMenuFlyout(file);
        
        var mediaPlayerWithStreamSource =
            await MediaPlayerWithStreamSource.CreateMediaPlayer(videoUrl, filesInfo: file);

        var mediaPlayer = mediaPlayerWithStreamSource.MediaPlayer;
        var mediaPlayerElement = new MediaPlayerElement()
        {
            ContextFlyout = menuFlyout,
            Tag = mediaPlayerWithStreamSource
        };

        mediaPlayerElement.SetMediaPlayer(mediaPlayer);

        // 是否自动播放
        if (AppSettings.IsAutoPlayInVideoDisplay)
        {
            mediaPlayerElement.AutoPlay = true;
        }

        mediaPlayerElement.MediaPlayer.MediaOpened += (sender, args) =>
        {
            if(_isDisposing) return;

            MediaPlayer_MediaOpened(sender, args);

            if (sender.PlaybackSession.NaturalVideoHeight > 2000)
            {
                _highBitRateVideos.Add(file.PickCode);
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                var transportControlsTemplateRoot = (FrameworkElement)VisualTreeHelper.GetChild(mediaPlayerElement.TransportControls, 0);
                var sliderControl = (Slider)transportControlsTemplateRoot.FindName("ProgressSlider");
                if (sliderControl != null && sender.PlaybackSession.NaturalDuration.TotalSeconds > 1000)
                {
                    // 十秒一步
                    sliderControl.StepFrequency = 1000 / sender.PlaybackSession.NaturalDuration.TotalSeconds;
                    sliderControl.SmallChange = 1000 / sender.PlaybackSession.NaturalDuration.TotalSeconds;
                }
            });

        };
        mediaPlayerElement.MediaPlayer.MediaEnded += MediaPlayer_MediaEnded;

        mediaPlayerElement.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

        if (addIndex == -1)
        {
            Video_UniformGrid.Children.Add(mediaPlayerElement);
        }
        else
        {
            Video_UniformGrid.Children.Insert(addIndex, mediaPlayerElement);
        }

    }

    private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
    {
        Debug.WriteLine("视频已经播放完了");
    }


    private static void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
    {
        // 是否需要改变起始位置
        if (AppSettings.AutoPlayPositionPercentage == 0.0) return;

        sender.Position = sender.NaturalDuration * AppSettings.AutoPlayPositionPercentage / 100;
    }

    private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine("播放失败");
    }


    private async void tryPlayVideos(List<FilesInfo> filesInfo)
    {
        ChangedVideo_UniformGrid(filesInfo.Count);

        PlayingVideoInfos.Clear();

        RemoveAllMediaControl();

        foreach (var file in filesInfo.Take(MaxCanPlayCount))
        {
            // 添加MediaElement
            await AddMediaElement(file);

            // 记录正在播放的视频
            PlayingVideoInfos.Add(file);
        }

        VideoPlay_Pivot.SelectedIndex = 1;

        // 搜索影片信息
        CidInfos.Clear();
        
        await FindAndShowInfosFromInternet(PlayingVideoInfos.ToList());


    }

    private async Task FindAndShowInfosFromInternet(List<FilesInfo> filesInfos)
    {
        VideoPlay_ListView.IsEnabled = false;
        Debug.WriteLine("正在搜索cid相应的信息");
        FindCidInfo_ProgressRing.Visibility = Visibility.Visible;

        const string noPicturePath = Const.Common.NoPicturePath;

        //搜刮
        foreach (var video in filesInfos)
        {
            Debug.WriteLine($"\t当前搜索：{video.Name}");

            Debug.WriteLine($"\t_isDisposing：{_isDisposing}");
            if (_isDisposing) return;

            VideoInfo cidInfo;
            var name = video.Name;
            var cid = FileMatch.MatchName(name);
            if (cid == null)
            {
                cidInfo = new VideoInfo { truename = name, imagepath = noPicturePath };
                CidInfos.Add(cidInfo);
                continue;
            }

            Debug.WriteLine("尝试从数据库中搜索");
            var result = DataAccess.SelectTrueName(cid);

            //数据库中有
            if (result.Count != 0)
            {
                Debug.WriteLine("数据库中存在该信息");

                //使用第一个符合条件的Name
                cidInfo = DataAccess.LoadOneVideoInfoByCID(result[0]);


                Debug.WriteLine($"\t>>{cidInfo.truename}");
            }
            //网络中查询
            else
            {
                Debug.WriteLine("数据库不存在该信息，尝试从网络中查找");
                var spiderManager = Spider.Manager.Current;

                cidInfo = await spiderManager.DispatchSpiderInfoByCIDInOrder(cid);

                Debug.WriteLine($"\t网络搜索到的结果{cidInfo?.truename}");
            }

            cidInfo ??= new VideoInfo { truename = cid, imagepath = noPicturePath };
            CidInfos.Add(cidInfo);

        }

        FindCidInfo_ProgressRing.Visibility = Visibility.Collapsed;

        Debug.WriteLine("完成搜索cid相应的信息");

        VideoPlay_ListView.IsEnabled = true;
    }

    private void DoubleVideoPlayButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Video_UniformGrid.Children)
        {
            var videoControl = item as MediaPlayerElement;
            videoControl.MediaPlayer.Play();
        }
    }

    private void DoubleVideoPauseButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in Video_UniformGrid.Children)
        {
            var videoControl = item as MediaPlayerElement;
            videoControl.MediaPlayer.Pause();
        }
    }

    private void IsMuteButton_Checked(object sender, RoutedEventArgs e)
    {
        foreach (var item in Video_UniformGrid.Children)
        {
            var videoControl = item as MediaPlayerElement;
            videoControl.MediaPlayer.IsMuted = true;

        }
    }

    private void IsMuteButton_Unchecked(object sender, RoutedEventArgs e)
    {
        foreach (var item in Video_UniformGrid.Children)
        {
            var videoControl = item as MediaPlayerElement;
            videoControl.MediaPlayer.IsMuted = false;
        }
    }

    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"进度条拖到 {e.NewValue}%");

        foreach (var item in Video_UniformGrid.Children)
        {
            var videoControl = item as MediaPlayerElement;

            videoControl.MediaPlayer.Position = videoControl.MediaPlayer.NaturalDuration * e.NewValue / 100;
        }
    }

    /// <summary>
    /// 从115中删除文件
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    private async Task DeletedFileFrom115Async(FilesInfo fileInfo)
    {
        // 首先，从播放列表中删除
        var fid = DeletedFileFromListAsync(fileInfo);
        if (fid == null) return;

        // 播放下一集（如果存在）
        await TryRemoveCurrentVideoAndPlayNextVideo(fileInfo);

        // 然后，删除115文件
        await webApi.DeleteFiles(fileInfo.Cid, new[] { (long)fid });

        // 接着，删除资源管理器的文件，如果存在（有可能已经关掉了）
        if (LastFilesListView.IsLoaded && LastFilesListView.ItemsSource is IncrementalLoadDatumCollection filesInfos &&
            filesInfos.Contains(fileInfo))
        {
            filesInfos.Remove(fileInfo);
        }

    }

    /// <summary>
    /// 从播放列表中删除
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <returns></returns>
    private long? DeletedFileFromListAsync(FilesInfo fileInfo)
    {
        var isFile = fileInfo.Type == FilesInfo.FileType.File;

        // 文件 fid
        // 文件夹 cid
        var fid = fileInfo.Id;

        //移除播放列表
        if (FilesInfos.Contains(fileInfo)) FilesInfos.Remove(fileInfo);

        // 文件
        if (isFile)
        {
            //移除正在播放的视频列表
            if (PlayingVideoInfos.Contains(fileInfo)) PlayingVideoInfos.Remove(fileInfo);

        }
        // 文件夹
        else
        {
            var playList = PlayingVideoInfos.Where(info => info.Id == fid).ToList();

            playList.ForEach(info =>
            {
                //移除正在播放的视频列表
                PlayingVideoInfos.Remove(info);
            });
        }

        RemoveCidInfo(fileInfo);

        return fid;
    }


    private void RemoveCidInfo(FilesInfo fileInfo)
    {
        //移除cid信息（预览图/信息）
        var removeCid = CidInfos.FirstOrDefault(item =>
            item.truename == fileInfo.Name || item.truename.ToUpper() == FileMatch.MatchName(fileInfo.Name)?.ToUpper());

        if (removeCid != null)
        {
            CidInfos.Remove(removeCid);
        }
    }

    private void RemovePlayingVideo(MediaPlayerElement mediaPlayerElement)
    {
        if (mediaPlayerElement.Tag is not MediaPlayerWithStreamSource oldMediaPlayerWithStreamSource) return;

        Debug.WriteLine("Dispose oldMediaPlayerWithStreamSource mediaPlayer");
        try
        {
            oldMediaPlayerWithStreamSource.Dispose();
            Debug.WriteLine("Dispose oldMediaPlayerWithStreamSource and mediaPlayer success");

            if (Video_UniformGrid.Children.Contains(mediaPlayerElement))
            {
                Debug.WriteLine("Remove mediaPlayerElement");
                Video_UniformGrid.Children.Remove(mediaPlayerElement);

                Debug.WriteLine("Remove mediaPlayerElement succeed");
            }

            if (_highBitRateVideos.Contains(oldMediaPlayerWithStreamSource.FilesInfo.PickCode))
            {
                _highBitRateVideos.Remove(oldMediaPlayerWithStreamSource.FilesInfo.PickCode);
            }

            Debug.WriteLine("成功移除正在播放的视频");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"删除正在播放的视频文件时出现问题{ex.Message}");
        }


    }

    private async Task<ContentDialogResult> TipDeletedFiles()
    {
        var dialog = new ContentDialog()
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "确认",
            PrimaryButtonText = "删除",
            CloseButtonText = "返回",
            DefaultButton = ContentDialogButton.Close,
            Content = "该操作将删除115网盘中的文件，确认删除？"
        };

        return await dialog.ShowAsync();
    }

    private void InfoGridVisibilityButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootSplitView.IsPaneOpen)
        {
            RootSplitView.IsPaneOpen = false;
            ((HyperlinkButton)sender).Content = "\uf743";
        }
        else
        {
            RootSplitView.IsPaneOpen = true;
            ((HyperlinkButton)sender).Content = "\uf745";
        }
    }

    private void Target_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void Target_DragEnter(object sender, DragEventArgs e)
    {
        // We don't want to show the Move icon
        e.DragUIOverride.IsGlyphVisible = false;
    }

    private void Target_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListView target) return;

        if (target.ItemsSource != FilesInfos) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is not List<FilesInfo> filesInfos) return;

        foreach (var fileInfo in filesInfos)
        {
            FilesInfos.Add(fileInfo);
        }
    }

    private void EmptyList_Click(object sender, RoutedEventArgs e)
    {
        FilesInfos.Clear();
    }

    private void VideoUniformGrid_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Grid) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is not List<Data.FilesInfo> filesInfos) return;

        FilesInfos.Clear();
        foreach (var info in filesInfos)
        {
            //if (info.Type == FilesInfo.FileType.File && info.IsVideo)

            FilesInfos.Add(info);
        }

        TryPlayVideoFromSelectedFiles(FilesInfos.ToList());

    }

    private void Link_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Link;
        e.DragUIOverride.Caption = "播放";
    }

    private void EnlargeButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button { DataContext: VideoInfo videoInfo }) return;

        ProtectedCursor = CursorHelper.GetZoomCursor();

        SmokeGrid.Visibility = Visibility.Visible;

        EnlargeImage.Source = new BitmapImage(new Uri(videoInfo.imagepath));

    }

    private void EnlargeButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ProtectedCursor = null;

        SmokeGrid.Visibility = Visibility.Collapsed;
    }


    private void CidInfoUserControlPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType is PointerDeviceType.Mouse or PointerDeviceType.Pen)
        {
            VisualStateManager.GoToState(sender as Control, "EnlargeButtonShown", true);
        }
    }

    private void CidInfoUserControlPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType is PointerDeviceType.Mouse or PointerDeviceType.Pen)
        {
            VisualStateManager.GoToState(sender as Control, "EnlargeButtonHidden", true);
        }
    }


    /// <summary>
    /// 从播放列表中移除
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void RemoveFileFromListButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: FilesInfo fileInfo }) return;

        Debug.WriteLine("开始从播放列表中删除");

        // 从播放列表中删除
        DeletedFileFromListAsync(fileInfo);

        Debug.WriteLine("完成一部分 从播放列表中删除");

        Debug.WriteLine("开始 自动播放下一视频");

        // 移除视频后，如果播放列表中有其余的视频则插入到播放中
        await TryRemoveCurrentVideoAndPlayNextVideo(fileInfo);



        Debug.WriteLine("完成从播放列表中删除");
    }

    private MediaPlayerElement GetFirstElementPlayPickCode(string pc)
    {
        var media = Video_UniformGrid.Children.FirstOrDefault(item =>
            ((item as MediaPlayerElement)?.Tag as MediaPlayerWithStreamSource)?.FilesInfo.PickCode == pc);
        if (media == null || media is not MediaPlayerElement mediaPlayerElement) return null;

        return mediaPlayerElement;
    }

    /// <summary>
    /// 自动播放下一视频
    /// </summary>
    /// <returns></returns>
    private async Task TryRemoveCurrentVideoAndPlayNextVideo(FilesInfo removeFileInfo)
    {
        // 即将删除的视频所属控件
        var mediaPlayerElement = GetFirstElementPlayPickCode(removeFileInfo.PickCode);

        if (mediaPlayerElement == null) return;

        //正在播放的文件的pc
        var playingPcList = Video_UniformGrid.Children.Select(element =>
            ((element as MediaPlayerElement)?.Tag as MediaPlayerWithStreamSource)?.FilesInfo.PickCode).ToList();

        // 挑选播放列表中不是正在播放的视频
        // 删除的一般只有一个，只取一个
        var videoInfo = FilesInfos.FirstOrDefault(item => item.IsVideo && !playingPcList.Contains(item.PickCode));

        if (videoInfo != null)
        {
            Debug.WriteLine("准备加载下一集");

            await RemoveVideoAndPlayNextVideo(mediaPlayerElement, videoInfo);

            Debug.WriteLine("添加到正在播放的视频的信息中");

            // 添加到正在播放的视频的信息中
            PlayingVideoInfos.Add(videoInfo);

            Debug.WriteLine("搜索信息");

            // 搜索信息
            await FindAndShowInfosFromInternet(new List<FilesInfo>() { videoInfo });
        }
        // 没有下一集，则修改布局
        else
        {
            Debug.WriteLine("未发现下一集");

            //移除正在播放的视频
            RemovePlayingVideo(mediaPlayerElement);

            Debug.WriteLine("播放的视频数量减小，修改布局");
            // 修改布局
            ChangedVideo_UniformGrid(Video_UniformGrid.Children.Count);
        }
    }

    ///// <summary>
    ///// 修改UriSource并移到最后
    ///// </summary>
    ///// <param name="mediaPlayerElement"></param>
    ///// <param name="videoInfo"></param>
    ///// <returns></returns>
    //private async Task ChangedVideoSourceAndMoveToLast(MediaPlayerElement mediaPlayerElement, FilesInfo videoInfo)
    //{
    //    Debug.WriteLine("正在切换UriSource");

    //    //将即将删除的视频移到最后
    //    Video_UniformGrid.Children.Move((uint)Video_UniformGrid.Children.IndexOf(mediaPlayerElement),
    //        (uint)Video_UniformGrid.Children.Count - 1);

    //    var videoUrl = await GetVideoUrl(videoInfo);
    //    if (videoUrl == null) return;

    //    var menuFlyout = BuildMenuFlyout(videoInfo);

    //    var mediaPlayerWithStreamSource =
    //        await MediaPlayerWithStreamSource.CreateMediaPlayer(videoUrl, filesInfo: videoInfo);

    //    var mediaPlayer = mediaPlayerWithStreamSource.MediaPlayer;
    //    mediaPlayer.CurrentStateChanged += MediaPlayer_CurrentStateChanged;

    //    mediaPlayerElement.Tag = mediaPlayerElement;
    //    mediaPlayerElement.ContextFlyout = menuFlyout;

    //    mediaPlayerElement.SetMediaPlayer(mediaPlayerWithStreamSource.MediaPlayer);

    //    Debug.WriteLine("成功切换UriSource");
    //}

    private void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
    {
        Debug.WriteLine("当前的状态改变了");
    }

    /// <summary>
    /// 删除后重新添加
    /// </summary>
    /// <param name="mediaPlayerElement"></param>
    /// <param name="videoInfo"></param>
    /// <returns></returns>
    private async Task RemoveVideoAndPlayNextVideo(MediaPlayerElement mediaPlayerElement, FilesInfo videoInfo)
    {
        RemovePlayingVideo(mediaPlayerElement);

        Debug.WriteLine($"当前高质量视频数量为：{_highBitRateVideos.Count}");
        // 避免同时请求过多
        if (_highBitRateVideos.Count > 2)
        {
            Debug.WriteLine("等待3秒");
            await Task.Delay(3000);
        }

        await AddMediaElement(videoInfo);
    }


    /// <summary>
    /// 从115中删除
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void RemoveFileFrom115Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: FilesInfo fileInfo }) return;

        var result = await TipDeletedFiles();

        if (result == ContentDialogResult.Primary)
        {
            await DeletedFileFrom115Async(fileInfo);
        }
    }

    /// <summary>
    /// 重新加载视频
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void LoadVideoAgainButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: FilesInfo fileInfo }) return;

        //获取正在播放该视频的控件
        var oldMediaElement = GetFirstElementPlayPickCode(fileInfo.PickCode);
        if (oldMediaElement == null) return;

        if (oldMediaElement.Tag is not MediaPlayerWithStreamSource oldMediaPlayerWithStreamSource) return;

        var videoUrl = oldMediaPlayerWithStreamSource.Url;

        // 删除旧的
        oldMediaPlayerWithStreamSource.Dispose();
        var index = Video_UniformGrid.Children.IndexOf(oldMediaElement);
        Video_UniformGrid.Children.RemoveAt(index);

        // 添加新的
        await AddMediaElement(fileInfo, videoUrl, index);

    }

    private System.Timers.Timer aTimer;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Video_UniformGrid_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        aTimer?.Stop();

        // Create a timer with a one second interval.
        aTimer = new System.Timers.Timer(500);
        aTimer.Enabled = true;
        aTimer.Elapsed += timer_Tick; ;
    }

    //鼠标状态计数器
    private int _iCount = 0;

    private void timer_Tick(object sender, System.Timers.ElapsedEventArgs e)
    {
        //鼠标状态计数器>=0的情况下鼠标可见，<0不可见，并不是直接受api函数影响而改变
        var i = CursorHelper.GetIdleTick();

        if (i > 4000)
        {
            while (_iCount >= 0)
            {
                Debug.WriteLine($"隐藏鼠标 ({_iCount}）");
                _iCount = CursorHelper.ShowCursor(false);
                TryUpdateUi(false);
            }

        }
        else
        {
            while (_iCount < 0)
            {
                Debug.WriteLine($"显示鼠标 ({_iCount}）");
                _iCount = CursorHelper.ShowCursor(true);
                TryUpdateUi();
            }
        }
    }

    private void TryUpdateUi(bool isOpenSplitPane = true)
    {
        var cursor = isOpenSplitPane ? null : CursorHelper.GetHiddenCursor();
        var visibility = isOpenSplitPane ? Visibility.Visible: Visibility.Collapsed;

        if (DispatcherQueue.HasThreadAccess)
        {
            ProtectedCursor = cursor;
            SplitViewOpenButton.Visibility = visibility;
        }
        else
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ProtectedCursor = cursor;
                SplitViewOpenButton.Visibility = visibility;
            });
        }
    }

    private void Video_UniformGrid_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        aTimer?.Stop();

        ProtectedCursor = null;
    }

    private void LocationFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: FilesInfo info }) return;

        // 接着，删除资源管理器的文件，如果存在（有可能已经关掉了）
        if (LastFilesListView.IsLoaded && LastFilesListView.ItemsSource is IncrementalLoadDatumCollection filesInfos &&
            filesInfos.Contains(info))
        {
            LastFilesListView.ScrollIntoView(info, ScrollIntoViewAlignment.Leading);
        }
    }
}
