﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using ByteSizeLib;
using DataAccess.Dao.Interface;
using DataAccess.Models.Entity;
using Display.Helper.FileProperties.Name;
using Display.Helper.Network;
using Display.Models.Api.OneOneFive.File;
using Display.Models.Dto.Media;
using Display.Models.Enums;
using Display.Models.Vo;
using Display.Models.Vo.IncrementalCollection;
using Display.Models.Vo.OneOneFive;
using Display.Providers;
using Display.ViewModels;
using Display.Views.Pages.More.Import115DataToLocalDataAccess;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SharpCompress;

namespace Display.Views.Pages.More.DatumList;

public sealed partial class FileListPage : INotifyPropertyChanged
{
    private const string UpSortIconRun = "\uE014";
    private const string DownSortIconRun = "\uE015";
    private WebApi _webApi;
    private readonly ObservableCollection<ExplorerItem> _units;
    private static readonly ExplorerItem RootExplorerItem = new()
    {
        Name = "根目录",
        Id = 0
    };
    private ExplorerItem CurrentExplorerItem => _units.LastOrDefault();
    private ExplorerItem LastExplorerItem => _units.Count <= 1 ? RootExplorerItem : _units[^2];

    private IncrementalLoadDatumCollection _filesInfos;
    private IncrementalLoadDatumCollection FilesInfos
    {
        get => _filesInfos;
        set
        {
            if (_filesInfos == value) return;
            _filesInfos = value;

            OnPropertyChanged();
        }
    }

    private WebApi WebApi => _webApi ??= WebApi.GlobalWebApi;

    private bool _isLoading = true;

    private readonly IFilesInfoDao _filesInfoDao = App.GetService<IFilesInfoDao>();
    

    /// <summary>
    /// 中转站文件
    /// </summary>
    private ObservableCollection<TransferStationFiles> _transferStationFiles;

    public FileListPage()
    {
        _units = [];
        InitializeComponent();

        InitData(0);
    }

    public FileListPage(long cid)
    {
        _units = [];
        InitializeComponent();

        InitData(cid);
    }

    private void InitData(long cid)
    {
        NavigationCacheMode = NavigationCacheMode.Enabled;

        MyProgressBar.Visibility = Visibility.Visible;

        FilesInfos = new IncrementalLoadDatumCollection(cid);

        MetadataControl.ItemsSource = _units;
        BaseExample.ItemsSource = FilesInfos;
        FilesInfos.GetFileInfoCompleted += FilesInfos_GetFileInfoCompleted;
        FilesInfos.WebPathChanged += FilesInfos_WebPathChanged;
    }

    private void FilesInfos_WebPathChanged(WebPath[] obj)
    {
        _units.Clear();
        FilesInfos.WebPaths?.ForEach(path => _units.Add(new ExplorerItem { Name = path.Name, Id = path.Cid }));

    }

    private void FilesInfos_GetFileInfoCompleted(object sender, GetFileInfoCompletedEventArgs e)
    {
        ChangedOrderIcon(e.OrderBy, e.Asc);
        MyProgressBar.Visibility = Visibility.Collapsed;
        _isLoading = false;
    }

    private async Task OpenFolder(long cid)
    {
        await FilesInfos.SetCid(cid);

        // 切换目录时，全选checkBox不是选中状态
        MultipleSelectedCheckBox.IsChecked = false;
    }

    private async void GoToFolder(DetailFileInfo detailFileInfo)
    {
        _isLoading = true;

        //跳过文件
        if (detailFileInfo.Type == FileType.File) return;
        if (detailFileInfo.Id == default) return;


        var id = detailFileInfo.Id;

        await FilesInfos.SetCid(id);

        // 切换目录时，全选checkBox不是选中状态
        MultipleSelectedCheckBox.IsChecked = false;

        ChangedMenuState();
    }

    private void OpenFolder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not TextBlock { DataContext: DetailFileInfo filesInfo }) return;

        GoToFolder(filesInfo);
    }

    private void ToTopButton_Click(object sender, RoutedEventArgs e)
    {
        if (BaseExample.ItemsSource is IncrementalLoadDatumCollection collection)
            BaseExample.ScrollIntoView(collection.First());
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (BaseExample.SelectedItems is null || BaseExample.SelectedItems.Count == 0)
        {
            ShowTeachingTip("当前未选中文件");
            return;
        }

        var filesInfo = BaseExample.SelectedItems.Cast<DetailFileInfo>().ToList();

        var page = new DatumList.VideoDisplay.MainPage(filesInfo, BaseExample);
        page.CreateWindow();
    }

    private void OrderBy_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock) return;

        if (textBlock.Inlines.FirstOrDefault() is not Run run) return;

        switch (run.Text)
        {
            case "名称":
                ChangedOrder(WebApi.OrderBy.FileName, NameRun);
                break;
            case "修改时间":
                ChangedOrder(WebApi.OrderBy.UserProduceTime, TimeRun);
                break;
            case "大小":
                ChangedOrder(WebApi.OrderBy.FileSize, SizeRun);
                break;
        }
    }

    private async void ChangedOrder(WebApi.OrderBy orderBy, Run run)
    {
        var asc = run.Text == UpSortIconRun ? 0 : 1;

        await FilesInfos.SetOrder(orderBy, asc);
    }

    private void ChangedOrderIcon(WebApi.OrderBy orderBy, int asc)
    {

        Run[] orderIconRunList = [TimeRun, NameRun, SizeRun];

        Run run = orderBy switch
        {
            WebApi.OrderBy.FileName => NameRun,
            WebApi.OrderBy.UserProduceTime => TimeRun,
            WebApi.OrderBy.FileSize => SizeRun,
            _ => TimeRun
        };

        foreach (var itemRun in orderIconRunList)
        {
            if (itemRun == run)
            {
                itemRun.Text = asc == 1 ? UpSortIconRun : DownSortIconRun;
            }
            else if (itemRun.Text != string.Empty)
            {
                itemRun.Text = string.Empty;
            }
        }
    }

    private void Source_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {

        // 排除缺失Id的文件，比如秒传后临时添加的文件
        var infos = e.Items.Cast<DetailFileInfo>().Where(x => !x.NoId).ToList();
        if (infos.Count == 0) return;

        // 添加数据
        e.Data.Properties.Add("items", infos);

        // 显示回收站
        RecycleStationGrid.Visibility = Visibility.Visible;

        // 显示中转站
        TransferStationGrid.Visibility = Visibility.Visible;

    }

    /// <summary>
    /// 拖拽文件，在中转站上松开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TransferStationGrid_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Grid) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is not List<DetailFileInfo> sourceFilesInfos) return;

        if (_transferStationFiles == null)
        {
            _transferStationFiles = [];
            TransferStationListView.ItemsSource = _transferStationFiles;
        }

        _transferStationFiles.Add(new TransferStationFiles(sourceFilesInfos));

    }

    /// <summary>
    /// 拖拽文件，在回收站上不松开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DeletedFileMove_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Link;

        e.DragUIOverride.Caption = "删除";
    }

    /// <summary>
    /// 拖拽文件，在文件列表上方不松开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FileMove_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListView target) return;


        // 应用内的文件拖动
        if (e.DataView.Properties.Values.FirstOrDefault() is List<DetailFileInfo> sourceFilesInfos)
        {
            HandleCaption(e, target, sourceFilesInfos);
        }
        //从外部拖入文件信息
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = "松开后开始上传";
        }

        VisualStateManager.GoToState(this, "TransferPointerOver", true);

    }

    private void HandleCaption(DragEventArgs e, ListView target, ICollection<DetailFileInfo> sourceFilesInfos)
    {
        var index = GetInsertIndexInListView(target, e);
        // 范围之外
        if (index == -1)
        {
            // 文件从中转站中拖拽过来，允许拖拽文件到此处
            if (!FilesInfos.Contains(sourceFilesInfos.FirstOrDefault()))
            {
                e.AcceptedOperation = DataPackageOperation.Move;
            }

            //否则禁止操作
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
                e.DragUIOverride.Caption = null;
            }
        }
        else
        {
            var item = FilesInfos[index];
            // 目标与移动文件有重合，退出
            if (sourceFilesInfos.Contains(item))
            {
                e.AcceptedOperation = DataPackageOperation.None;
                e.DragUIOverride.Caption = null;
            }
            // 目标为文件，没法移动到文件，退出
            else if (item.Type == FileType.File)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                e.DragUIOverride.Caption = null;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = $"移动到 {item.Name}";
            }
        }
    }

    /// <summary>
    /// 拖拽文件，在中转站上方不松开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void TransferMove_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not Grid) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is not List<DetailFileInfo> sourceFilesInfos) return;

        if (_transferStationFiles == null)
        {
            e.AcceptedOperation = DataPackageOperation.Link;
        }
        else
        {
            // 如果移动的文件已经在中转站了，没必要再移动了
            var sameFile = _transferStationFiles.Where(item =>
            {
                return item.TransferFiles.All(file => sourceFilesInfos.Contains(file));
            }).FirstOrDefault();

            if (sameFile == null)
            {
                e.AcceptedOperation = DataPackageOperation.Link;
            }
        }

    }

    /// <summary>
    /// 点击清空中转站
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void EmptyTransferStationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_transferStationFiles == null) return;

        _transferStationFiles.Clear();

        TransferStationGrid.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// 拖拽文件，在回收站上松开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void RecycleStationGrid_Drop(object sender, DragEventArgs e)
    {
        if (sender is not Grid) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is not List<DetailFileInfo> sourceFilesInfos) return;

        //115删除
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "确认",
            PrimaryButtonText = "删除",
            CloseButtonText = "返回",
            DefaultButton = ContentDialogButton.Close,
            Content = "该操作将删除115网盘中的文件，确认删除？"
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            await Delete115Files(sourceFilesInfos);
        }

        VisualStateManager.GoToState(this, "NoDelete", true);
    }

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="sourceFilesInfos"></param>
    /// <returns></returns>
    private async Task Delete115Files(IList<DetailFileInfo> sourceFilesInfos)
    {
        if (sourceFilesInfos == null) return;
        
        // 删除文件列表中的文件
        TryRemoveFilesInExplorer(sourceFilesInfos);

        //// 删除中转站中的文件
        //TryRemoveTransferFiles(sourceFilesInfos);

        var cid = sourceFilesInfos.First().Cid;

        // 从115中删除
        var result = await WebApi.DeleteFiles(cid,
             sourceFilesInfos.Where(item => item.Id != default).Select(item => item.Id).ToArray());

        if (!result) ShowTeachingTip("删除文件失败");
    }

    private void RecycleStationGrid_DragEnter(object sender, DragEventArgs e)
    {
        VisualStateManager.GoToState(this, "ReadyDelete", true);
    }

    private void RecycleStationGrid_DragLeave(object sender, DragEventArgs e)
    {
        VisualStateManager.GoToState(this, "NoDelete", true);
    }

    /// <summary>
    /// 拖拽文件，在文件列表上松开
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void BaseExample_Drop(object sender, DragEventArgs e)
    {
        if (sender is not ListView target) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is List<DetailFileInfo> sourceFilesInfos)
        {
            await HandleFileDrop(e, target, sourceFilesInfos);
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();

            var storageFiles = items.Where(i => i.IsOfType(StorageItemTypes.File) && i is StorageFile).Select(x => x as StorageFile).ToArray();

            if (storageFiles.Length == 0) return;

            // 标记当前文件夹，避免切换文件夹后才上传成功导致的错误文件（UI）添加，影响显示
            var currentFolderCid = CurrentExplorerItem.Id;

            var uploadViewModel = App.GetService<UploadViewModel>();

            foreach (var storageFile in storageFiles)
            {
                uploadViewModel.AddUploadTask(storageFile.Path, currentFolderCid, result =>
                {
                    if (!result.Success) return;

                    if (currentFolderCid != CurrentExplorerItem.Id) return;

                    // 上传成功后更新UI
                    FilesInfos.Insert(0, new DetailFileInfo(result));
                });
            }

            //添加任务后显示传输任务窗口
            TaskPage.ShowSingleWindow();
        }
    }

    private async Task HandleFileDrop(DragEventArgs e, ListView target, List<DetailFileInfo> sourceFilesInfos)
    {
        var index = GetInsertIndexInListView(target, e);

        //在范围之外
        if (index == -1)
        {
            // 文件从中转站中拖拽过来，允许拖拽文件到此处
            if (!FilesInfos.Contains(sourceFilesInfos.FirstOrDefault()))
            {
                await Move115Files(FilesInfos.Cid, sourceFilesInfos);

                // 在文件列表中添加
                FilesInfos.AddArray(sourceFilesInfos.ToArray());
            }
            else
            {
                Debug.WriteLine("在范围之外，退出");
            }
        }
        else
        {
            var folderInfo = FilesInfos[index];

            //目标与移动文件有重合，退出
            if (sourceFilesInfos.Contains(folderInfo))
            {
                Debug.WriteLine("目标与移动文件有重合，退出");
                return;
            }

            if (folderInfo.Id == default) return;

            await Move115Files(folderInfo.Id, sourceFilesInfos);
        }
    }

    /// <summary>
    /// 删除文件列表中的文件
    /// </summary>
    /// <param name="files"></param>
    private void TryRemoveFilesInExplorer(IEnumerable<DetailFileInfo> files)
    {
        foreach (var item in files.Where(FilesInfos.Contains))
        {
            FilesInfos.Remove(item);
        }
    }

    public void RemoveFileById(long id)
    {
        var info = FilesInfos.FirstOrDefault(info => info.Id == id);
        if (info == null) return;

        FilesInfos.Remove(info);
    }

    private void TryRemoveFilesInTransfer(IEnumerable<DetailFileInfo> files)
    {
        if (_transferStationFiles is not { Count: > 0 }) return;

        var transferListReadyRemove = _transferStationFiles.Where(item => item.TransferFiles.All(files.Contains)).ToList();

        foreach (var file in transferListReadyRemove)
        {
            _transferStationFiles.Remove(file);
        }
    }

    /// <summary>
    /// 移动115文件
    /// </summary>
    /// <returns></returns>
    private async Task Move115Files(long cid, IList<DetailFileInfo> files)
    {
        await WebApi.MoveFiles(cid, files.Select(item => item.Id).ToArray());

        //// 从中转站列表中删除已经移动的文件
        //TryRemoveTransferFiles(files);
    }

    private int GetInsertIndexInListView(ListView target, DragEventArgs e)
    {
        // Find the insertion index:
        var pos = e.GetPosition(target.ItemsPanelRoot);

        var index = -1;
        if (target.Items.Count == 0) return index;

        // Get a reference to the first item in the ListView
        var sampleItem = (ListViewItem)target.ContainerFromIndex(0);

        // Adjust itemHeight for margins
        var itemHeight = sampleItem.ActualHeight + sampleItem.Margin.Top + sampleItem.Margin.Bottom;

        // Find index based on dividing number of items by height of each item
        var tmp = (int)(pos.Y / itemHeight);

        if (tmp <= target.Items.Count - 1)
        {
            index = tmp;
        }

        return index;
    }

    private void FilesTransferStation_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        // 显示回收站
        RecycleStationGrid.Visibility = Visibility.Visible;

        // 添加数据
        var infos = e.Items.Cast<TransferStationFiles>().ToArray();
        e.Data.Properties.Add("items", TransferStationFilesToFilesInfo(infos));
    }

    private List<DetailFileInfo> TransferStationFilesToFilesInfo(TransferStationFiles[] srcList)
    {
        List<DetailFileInfo> infos = new();
        foreach (var item in srcList)
        {
            infos.AddRange(item.TransferFiles);
        }

        return infos;
    }

    private void Source_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {

        // 隐藏回收站
        RecycleStationGrid.Visibility = Visibility.Collapsed;

        // 中转站为空时隐藏
        if (_transferStationFiles == null || _transferStationFiles.Count == 0)
        {
            TransferStationGrid.Visibility = Visibility.Collapsed;
        }

        // 移动成功
        if (args.DropResult != DataPackageOperation.Move) return;

        // 删除文件列表
        TryRemoveFilesInExplorer(args.Items.Cast<DetailFileInfo>().ToList());

    }

    private async void ImportDataButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button) return;

        List<DetailFileInfo> files = [];
        List<string> folderList = [];
        foreach (DetailFileInfo item in BaseExample.SelectedItems)
        {
            files.Add(item);

            if (item.Type == FileType.Folder)
            {
                folderList.Add(item.Name);
            }
        }

        if (files.Count == 0)
        {
            ShowTeachingTip("当前未选中文件");
            return;
        }

        //确认对话框
        var isContinue = true;
        if (folderList.Count > 0)
        {
            var receiveResult = await ShowContentDialog(folderList);
            if (receiveResult != ContentDialogResult.Primary) isContinue = false;
        }

        if (!isContinue) return;

        var page = new Progress(files);
        page.CreateWindow();

    }

    /// <summary>
    /// 显示确认提示框
    /// </summary>
    /// <param name="nameList"></param>
    /// <returns></returns>
    private async Task<ContentDialogResult> ShowContentDialog(List<string> nameList)
    {
        var readyStackPanel = new StackPanel();

        readyStackPanel.Children.Add(new TextBlock { Text = "选中文件夹：" });
        var index = 0;
        foreach (var name in nameList)
        {
            index++;
            var textBlock = new TextBlock
            {
                Text = $"  {index}.{name}",
                IsTextSelectionEnabled = true,
                Margin = new Thickness(0, 2, 0, 0)
            };

            readyStackPanel.Children.Add(textBlock);
        }

        readyStackPanel.Children.Add(
            new TextBlock
            {
                Text = "未进入隐藏系统的情况下，隐藏的文件将被跳过，请知晓",
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.LightGray),
                MaxWidth = 300,
                Margin = new Thickness(0, 8, 0, 0)
            });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "确认后继续",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            Content = readyStackPanel
        };

        var result = await dialog.ShowAsync();

        return result;
    }

    private async void deleteData_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "确认后继续",
            PrimaryButtonText = "继续",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close
        };

        RichTextBlock textHighlightingRichTextBlock = new();

        Paragraph paragraph = new();
        paragraph.Inlines.Add(new Run { Text = "该操作将" });
        paragraph.Inlines.Add(new Run { Text = "删除", Foreground = new SolidColorBrush(Colors.OrangeRed), FontWeight = FontWeights.Bold, FontSize = 15 });
        paragraph.Inlines.Add(new Run { Text = "之前导入的" });
        paragraph.Inlines.Add(new Run { Text = "所有", Foreground = new SolidColorBrush(Colors.OrangeRed) });
        paragraph.Inlines.Add(new Run { Text = "115数据" });

        textHighlightingRichTextBlock.Blocks.Add(paragraph);

        dialog.Content = textHighlightingRichTextBlock;

        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        _filesInfoDao.Delete();
    }

    private async void DownButton_Click(object sender, RoutedEventArgs e)
    {
        await DownFiles(WebApi.DownType.Bc);
    }

    private async void Aria2Down_Click(object sender, RoutedEventArgs e)
    {
        await DownFiles(WebApi.DownType.Aria2);
    }

    private async void Browser115Down_Click(object sender, RoutedEventArgs e)
    {
        await DownFiles(WebApi.DownType._115);
    }

    private async Task DownFiles(WebApi.DownType downType)
    {
        if (BaseExample.SelectedItems is null || BaseExample.SelectedItems.Count == 0)
        {
            ShowTeachingTip("当前未选中文件");
            return;
        }

        if (BaseExample.SelectedItems.FirstOrDefault() is not DetailFileInfo) return;

        List<FilesInfo> videoInfos = [];

        foreach (var item in BaseExample.SelectedItems)
        {
            if (item is not DetailFileInfo fileInfo) continue;

            FilesInfo datum = new()
            {
                CurrentId = fileInfo.Cid,
                Name = fileInfo.Name,
                PickCode = fileInfo.PickCode,
                FileId = fileInfo.Id
            };
            videoInfos.Add(datum);
        }

        //BitComet只需要cid,n,pc三个值
        var isSuccess = await WebApi.RequestDown(videoInfos, downType);

        if (!isSuccess)
            ShowTeachingTip("请求下载失败");
    }

    private void ShowTeachingTip(string message)
    {
        BasePage.ShowTeachingTip(lightDismissTeachingTip: LightDismissTeachingTip, message);
    }

    private async void PlayVideoButton_Click(object sender, RoutedEventArgs e)
    {
        // 多个播放
        if (sender is not MenuFlyoutItem menuFlyoutItem) return;
        if (menuFlyoutItem.DataContext is null)
        {
            if (!BaseExample.SelectedItems.Any())
            {
                ShowTeachingTip("当前未选中文件");
                return;
            }

            // 挑选视频文件，并转换为mediaPlayItem
            var mediaPlayItems = BaseExample.SelectedItems.Cast<DetailFileInfo>()
                .Where(x => x.Type == FileType.Folder || x.IsVideo)
                .Select(x => new MediaPlayItem(x)).ToList();

            await PlayVideoHelper.PlayVideo(mediaPlayItems, XamlRoot, lastPage: this);

            return;
        }

        // 单个播放
        if (menuFlyoutItem.DataContext is not DetailFileInfo info) return;

        if (info.Type == FileType.Folder || !info.IsVideo) return;

        var mediaPlayItem = new MediaPlayItem(info);
        await PlayVideoHelper.PlayVideo(new List<MediaPlayItem> { mediaPlayItem }, XamlRoot, lastPage: this);
    }

    private void Sort115Button_Click(object sender, RoutedEventArgs e)
    {
        //检查选中的文件或文件夹
        if (BaseExample.SelectedItems.FirstOrDefault() is not DetailFileInfo) return;

        //获取需要整理的文件
        var folders = BaseExample.SelectedItems.Cast<DetailFileInfo>().ToList();

        var page = new Sort115.MainPage(folders);
        page.CreateWindow();
    }

    private async void PlayWithPlayerButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: PlayerType playerType } menuFlyoutItem) return;

        List<MediaPlayItem> mediaPlayItems;
        // 选项栏选中多个时，播放选中的
        if (BaseExample.SelectedItems.Count > 1)
        {
            //获取需要播放的文件
            mediaPlayItems = BaseExample.SelectedItems.Cast<DetailFileInfo>()
                .Where(x => x.Type == FileType.Folder || x.IsVideo)
                .Select(x => new MediaPlayItem(x)).ToList();
        }
        // 选项栏只选中一个，则忽略。以右键项为准
        else
        {
            // 单个播放
            if (menuFlyoutItem is not { DataContext: DetailFileInfo info }) return;

            if (info.Type == FileType.Folder || !info.IsVideo) return;

            mediaPlayItems = [new(info)];
        }


        await PlayVideoHelper.PlayVideo(mediaPlayItems, XamlRoot, lastPage: this, playerType: playerType);
    }

    private async void MoveToNewFolderItemClick(object sender, RoutedEventArgs e)
    {
        List<DetailFileInfo> fileInfos;
        // 选中文件，对当前文件操作
        if (BaseExample.SelectedItems.Count == 0)
        {
            if (sender is not MenuFlyoutItem { DataContext: DetailFileInfo info }) return;

            fileInfos = [info];

        }
        else
        {
            //获取需要整理的文件
            fileInfos = BaseExample.SelectedItems.Cast<DetailFileInfo>().ToList();

        }

        if (fileInfos.Count == 0) return;

        var currentFolderId = CurrentExplorerItem.Id;

        // 移除前缀
        var infos = fileInfos.Select(x =>
                            Regex.Replace(x.NameWithoutExtension, "\\w+.(com|cn|xyz|la|me|net|app|cc)@", string.Empty, RegexOptions.IgnoreCase))
                            .ToArray();

        var sameName = MatchHelper.GetSameFirstStringFromList(infos);

        // 移除后缀
        sameName = Regex.Replace(sameName, "[-_.]part$", string.Empty, RegexOptions.IgnoreCase);

        TextBox inputTextBox = new()
        {
            Text = sameName,
            PlaceholderText = "输入新文件夹的名称"
        };

        ContentDialog contentDialog = new()
        {
            XamlRoot = XamlRoot,
            Content = inputTextBox,
            Title = "新文件夹",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await contentDialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        // 新建文件夹
        var makeDirResult = await WebApi.RequestMakeDir(currentFolderId, inputTextBox.Text);
        if (makeDirResult == null) return;

        // 移动文件到新文件夹
        Debug.WriteLine($"移动文件数量：{fileInfos.Count}");
        await Move115Files(makeDirResult.Cid, fileInfos);

        // 更新UI
        // 新建文件夹
        FilesInfos.Insert(0, new DetailFileInfo(new FilesInfo { CurrentId = makeDirResult.Cid, Name = makeDirResult.Cname, ParentId = LastExplorerItem.Id, TimeEdit = (int)DateTimeOffset.Now.ToUnixTimeSeconds() }));

        // 删除文件
        TryRemoveFilesInExplorer(fileInfos);
    }

    private async void RenameItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: DetailFileInfo info }) return;

        var stackPanel = new StackPanel
        {
            Spacing = 3
        };

        var inputTextBox = new TextBox
        {
            Text = info.NameWithoutExtension,
            PlaceholderText = info.NameWithoutExtension,
            SelectionStart = info.NameWithoutExtension.Length
        };
        stackPanel.Children.Add(inputTextBox);

        CheckBox checkBox = null;
        if (info.Type == FileType.File)
        {
            checkBox = new CheckBox
            {
                Content = "强制重命名（包括后缀名）",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            ToolTipService.SetToolTip(checkBox, "按新名称重新上传文件，完成后删除旧文件");
            stackPanel.Children.Add(checkBox);
        }

        ContentDialog contentDialog = new()
        {
            XamlRoot = XamlRoot,
            Content = stackPanel,
            Title = "重命名",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await contentDialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;

        var isForceRename = checkBox?.IsChecked == true;

        // 添加后缀，模仿官方
        var inputText = inputTextBox.Text;

        // 强制重命名新名称就是输入框Text
        var newName = isForceRename ? inputText : string.IsNullOrEmpty(info.Datum.Ico) ? inputText : $"{inputText}.{info.Datum.Ico}";

        //没变
        if (newName == info.Name) return;

        bool isSucceed;

        // 强制重命名
        if (isForceRename)
        {
            isSucceed = await WebApi.RenameForce(info, newName);
        }
        // 一般重命名
        else
        {
            var renameRequest = await WebApi.RenameFile(info.Id, newName);
            isSucceed = renameRequest.State;
        }

        if (!isSucceed)
        {
            ShowTeachingTip("重命名失败");
            return;
        }

        // 强制重命名可能会影响图标
        info.UpdateName(newName, isForceRename);
    }

    private async void FolderBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Item is not ExplorerItem item) return;

        await OpenFolder(item.Id);
    }

    private void MetadataItem_OnDragOver(object sender, DragEventArgs e)
    {
        if (sender is not TextBlock { DataContext: ExplorerItem item }) return;

        // 拖拽中的文件（可能多个）
        if (e.DataView.Properties.Values.FirstOrDefault() is not List<DetailFileInfo> sourceFilesInfos) return;

        // 目标cid
        var cid = item.Id;

        var canMoveList = GetFilesInfosExceptInCid(cid, sourceFilesInfos);

        if (canMoveList.Any())
        {
            e.AcceptedOperation = DataPackageOperation.Move;
            e.DragUIOverride.Caption = $"移动到 {item.Name}";
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
            e.DragUIOverride.Caption = null;
        }
    }

    private static List<DetailFileInfo> GetFilesInfosExceptInCid(long cid, IList<DetailFileInfo> infos)
    {
        return infos.Where(x => x.Cid != cid).ToList();
    }

    private async void MetadataItem_OnDrop(object sender, DragEventArgs e)
    {
        if (sender is not TextBlock { DataContext: ExplorerItem item }) return;

        // 拖拽中的文件（可能多个）
        if (e.DataView.Properties.Values.FirstOrDefault() is not List<DetailFileInfo> sourceFilesInfos) return;

        var canMoveList = GetFilesInfosExceptInCid(item.Id, sourceFilesInfos);

        await Move115Files(item.Id, canMoveList);

        // 移动到当前文件夹的时候，文件列表需要添加
        var index = _units.IndexOf(item);
        if (index == _units.Count - 1)
        {
            sourceFilesInfos.ForEach(x =>
                FilesInfos.Insert(0, x));
        }
    }

    private void TransferStationListView_OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // 隐藏回收站
        RecycleStationGrid.Visibility = Visibility.Collapsed;

        if (args.DropResult is not DataPackageOperation.Move) return;

        // 移动成功，移除对应已经移动的文件（如果存在）
        var infos = args.Items.Cast<TransferStationFiles>().ToArray();
        var infosFormat = TransferStationFilesToFilesInfo(infos);

        //// 从列表文件中删除
        //TryRemoveFilesInExplorer(infosFormat);

        // 从中转站列表中删除
        TryRemoveFilesInTransfer(infosFormat);

        //// 如果中转站为空，就隐藏（目前数量为移动的数量，移动后将为0）
        // 如果中转站为空，就隐藏
        if (_transferStationFiles.Count == 0)
        {
            TransferStationGrid.Visibility = Visibility.Collapsed;
        }
    }

    private void SelectedMultipleFilesCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox) return;

        if (checkBox.IsChecked is true)
        {
            BaseExample.SelectAll();
        }
        else
        {
            BaseExample.SelectedItems.Clear();
        }
    }

    private async void GoBack_KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_units.Count <= 1) return;

        var currentItem = _units[^2];
        await OpenFolder(currentItem.Id);
    }

    private async void ShowInfoClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: DetailFileInfo info }) return;

        string title;

        var infos = new Dictionary<string, string>
        {
            {"名称",info.Name }
        };

        if (info.Type == FileType.Folder)
        {
            infos.Add("id", info.Id.ToString());
            infos.Add("pickCode", info.PickCode);
            infos.Add("所在目录id", info.Cid.ToString());

            title = "文件夹属性";
        }
        else
        {
            infos.Add("id", info.Id.ToString());
            infos.Add("pickCode", info.PickCode);
            infos.Add("大小", ByteSize.FromBytes(info.Size).ToString("#.#"));
            infos.Add("sha1", info.Sha1);
            infos.Add("所在目录id", info.Cid.ToString());

            title = "文件属性";
        }

        await InfoPage.ShowInContentDialog(XamlRoot, infos, title);
    }

    private async void ShowFolderInfoClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: ExplorerItem info }) return;

        var infos = new Dictionary<string, string>
        {
            {"名称",info.Name },
            {"cid",info.Id.ToString() }
        };

        await InfoPage.ShowInContentDialog(XamlRoot, infos, "属性");
    }

    private async void RefreshFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: ExplorerItem info }) return;

        await OpenFolder(info.Id);
    }

    private async void Refresh_KeyboardAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        await OpenFolder(CurrentExplorerItem.Id);
    }

    private bool _isSelectedListView;

    private DateTime _lastTapTime = DateTime.MinValue;
    private DetailFileInfo _lastInfo;
    private async void BaseExample_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var nowTime = DateTime.Now;

        DetailFileInfo info = null;
        if (e.AddedItems.FirstOrDefault() is DetailFileInfo)
        {
            info = e.AddedItems.FirstOrDefault() as DetailFileInfo;
        }
        else if (e.RemovedItems.FirstOrDefault() is DetailFileInfo)
        {
            info = e.RemovedItems.FirstOrDefault() as DetailFileInfo;
        }

        if (info == null) return;

        // 双击事件
        if (_lastInfo == info && (nowTime - _lastTapTime).TotalMilliseconds < 300)  // 300毫秒内连续两次点击视为双击
        {
            _lastTapTime = nowTime;

            if (info.Type == FileType.Folder)
            {
                GoToFolder(info);
                return;
            }

            if (info.IsVideo)
            {
                // 只有文件能双击，文件夹Click后就跳转到新页面了
                var mediaPlayItem = new MediaPlayItem(info);

                _ = PlayVideoHelper.PlayVideo(new List<MediaPlayItem> { mediaPlayItem }, XamlRoot, lastPage: this);
            }
            else if (info.IsImage)
            {
                if (info.Id == default) return;

                NavigationToImagePage(FilesInfos.ToList(), info);
            }
            else
            {
                ShowTeachingTip("不支持打开该格式");

                return;
            }

            return;
        }
        _lastTapTime = nowTime;
        _lastInfo = info;

        // 单击事件
        await Task.Delay(310);
        if (_isLoading) return;

        ChangedMenuState(info.IsVideo);
    }

    private void ChangedMenuState(bool isVideo = false)
    {
        var isSelectedState = BaseExample.SelectedItems.Count > 0;

        // 没有选中的情况，如果全选按钮选中，则取消
        if (!isSelectedState && MultipleSelectedCheckBox.IsChecked == true)
        {
            MultipleSelectedCheckBox.IsChecked = false;
        }

        // 当前状态与之前的一致，退出
        if (_isSelectedListView == isSelectedState) return;
        _isSelectedListView = isSelectedState;

        VisualStateManager.GoToState(this, isSelectedState ? "Show" : "Hidden", true);

        if (isSelectedState)
        {
            if (isVideo) GetThumbnailButton.Visibility = Visibility.Visible;
        }
        else
        {
            GetThumbnailButton.Visibility = Visibility.Collapsed;
        }

    }

    #region 设置图片控件

    private void NavigationToImagePage(List<DetailFileInfo> files, DetailFileInfo currentInfo)
    {
        var images = files.Where(i => i.IsImage).ToList();
        var currentIndex = images.IndexOf(currentInfo);

        Frame.Navigate(typeof(ImagePage), Tuple.Create(images, currentIndex), new EntranceNavigationTransitionInfo());
    }

    #endregion


    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        // Raise the PropertyChanged event, passing the Name of the property whose value has changed.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenSearchContent();
    }

    private async Task OpenSearchContent()
    {
        if (!SearchTeachingTip.IsOpen)
        {
            SearchTeachingTip.IsOpen = true;
            await Task.Delay(TimeSpan.FromSeconds(0.05));
        }

        SearchBox.Focus(FocusState.Keyboard);
    }

    private async void SearchBoxOnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        SearchResultListView.Footer = null;
        var result = await WebApi.GetSearchResult(CurrentExplorerItem.Id, sender.Text);

        if (result == null)
        {
            ShowTeachingTip("搜索失败");
            return;
        }

        if (result.Data is not { Length: > 0 })
        {
            SearchResultListView.ItemsSource = null;
            SearchResultListView.Footer = new TextBlock { Text = "无结果" };
            return;
        }

        SearchResultListView.ItemsSource = result.Data.Select(x => new DetailFileInfo(x)).ToList();
    }

    private void SearchTeachingTip_OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
        Debug.WriteLine("Closed");
    }

    private async void SearchResultListView_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DetailFileInfo info) return;

        if (info.Id == default) return;

        // 打开文件夹
        if (info.Type == FileType.Folder)
        {
            if (info.Id == default) return;

            await OpenFolder(info.Id);
            return;
        }

        // 打开图片
        if (info.IsImage)
        {
            if (info.Id == default || SearchResultListView.ItemsSource is not List<DetailFileInfo> infos) return;

            NavigationToImagePage(infos, info);
            return;
        }

        if (!info.IsVideo)
        {
            ShowTeachingTip("不支持打开该格式");

            return;
        }

        // 视频文件
        var mediaPlayItem = new MediaPlayItem(info);

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal,
            () => _ = PlayVideoHelper.PlayVideo(new List<MediaPlayItem> { mediaPlayItem }, XamlRoot, lastPage: this));
    }

    private async void SearchBoxInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        await OpenSearchContent();
    }

    private async void OpenFolderInSearchResult_ItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { DataContext: DetailFileInfo info }) return;

        await OpenFolder(info.Cid);

    }

    private void TransferStationGrid_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "TransferPointerOver", true);
    }

    private void TransferStationGrid_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "TransferNormal", true);
    }

    private void GetThumbnailButton_Click(object sender, RoutedEventArgs e)
    {
        var infos = BaseExample.SelectedItems.Cast<DetailFileInfo>()
            .Where(x => x.Type == FileType.Folder || x.IsVideo).ToList();
        Frame.Navigate(typeof(ThumbnailPage), infos, new EntranceNavigationTransitionInfo());
    }
}