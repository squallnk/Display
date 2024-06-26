// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Display.Models.Api.OneOneFive.OfflineDown;
using Display.Models.Api.OneOneFive.Upload;
using Display.Models.Dto.OneOneFive;
using Display.Providers;
using Display.Views.Pages.More.DatumList;
using Display.Views.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Display.Views.Pages.OfflineDown;

public sealed partial class OfflineDownPage
{
    private readonly WebApi _webApi = WebApi.GlobalWebApi;

    private MatchCollection _matchLinkCollection;

    private UploadInfoResult _uploadInfoResult;

    private OfflineSpaceInfo _offlineSpaceInfo;

    public OfflineDownPage(string defaultLink)
    {
        InitializeComponent();

        InitData();

        LinkTextBox.Text = defaultLink;
        CheckLinkCount(defaultLink);
    }

    private async void InitData()
    {
        var downPathRequest = await _webApi.GetOfflineDownPath();

        if (!(downPathRequest?.Data?.Length > 0)) return;

        foreach (var datum in downPathRequest.Data)
        {
            DownPathComboBox.Items.Add(datum);
        }

        DownPathComboBox.SelectedIndex = 0;

    }

    private void TextBox_OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        CheckLinkCount(textBox.Text);
    }

    private void CheckLinkCount(string content)
    {
        _matchLinkCollection = Regex.Matches(content,
            @"((((http|https|ftp|ed2k):\/\/)|(magnet:\?xt=urn:btih:)).{3,}?)([\r\n]|$)");

        LinksCountRun.Text = _matchLinkCollection.Count.ToString();
    }

    public async void CreateOfflineDownRequest()
    {
        _uploadInfoResult ??= await _webApi.GetUploadInfo();

        if (_uploadInfoResult == null)
        {
            // 无法获取上传信息
            FailLoaded?.Invoke(this, new FailLoadedEventArgs("无法获取UploadInfo"));
            return;
        }

        _offlineSpaceInfo ??= await _webApi.GetOfflineSpaceInfo(_uploadInfoResult.UserKey, _uploadInfoResult.UserId);

        if (_offlineSpaceInfo == null)
        {
            // 无法获取离线空间信息
            FailLoaded?.Invoke(this, new FailLoadedEventArgs("无法获取OfflineSpaceInfo"));
            return;
        }

        // 没有磁力
        if (_matchLinkCollection.Count == 0) return;

        // 保存路径
        if (DownPathComboBox.SelectedItem is not OfflineDownPathData downPath) return;


        // 链接
        List<string> links = [];
        foreach (Match match in _matchLinkCollection)
        {
            links.Add(match.Groups[1].Value);
        }

        Debug.WriteLine("正在添加磁力任务");

        var result = await _webApi.AddTaskUrl(links, downPath.FileId, downPath.UserId, _offlineSpaceInfo.Sign,
            _offlineSpaceInfo.Time);

        Debug.WriteLine("添加磁力任务完毕");

        RequestCompleted?.Invoke(this, new RequestCompletedEventArgs(result));
        Debug.WriteLine("Invoke 添加磁力任务完毕");
    }

    public event EventHandler<FailLoadedEventArgs> FailLoaded;

    public event EventHandler<RequestCompletedEventArgs> RequestCompleted;

    private void RootGrid_OnDragOver(object sender, DragEventArgs e)
    {
        //获取拖入文件信息
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        e.AcceptedOperation = DataPackageOperation.Link;
        e.DragUIOverride.Caption = "拖拽torrent后开始任务";
    }


    private async void RootGrid_OnDrop(object sender, DragEventArgs e)
    {
        // 获取拖入文件信息
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;

        // 获取保存路径
        if (DownPathComboBox.SelectedItem is not OfflineDownPathData downPath) return;

        var items = await e.DataView.GetStorageItemsAsync();

        var rarItems = items.Where(i => i.IsOfType(StorageItemTypes.File) && i is StorageFile { FileType: ".torrent" }).Select(x => x as StorageFile).ToArray();

        var length = rarItems.Length;

        var isSucceed = false;
        string content;
        switch (length)
        {
            case 0:
                content = "未发现torrent文件";
                break;
            // 单个torrent
            case 1:
                (isSucceed, content) = await WebApi.GlobalWebApi.CreateTorrentOfflineDown(downPath.FileId, rarItems.First().Path);
                break;
            // 多个
            default:
            {
                var failCount = 0;
                for (var i = 1; i <= length; i++)
                {
                    var file = rarItems[i];

                    Debug.WriteLine(file.FileType);

                    ShowTeachingTip($"添加torrent任务中：{i}/{length}" + (failCount > 0 ? "，失败数:{failCount}" : string.Empty));

                    var (isCurrentSucceed, _) = await WebApi.GlobalWebApi.CreateTorrentOfflineDown(
                        downPath.FileId, file.Path);

                    if (!isCurrentSucceed) failCount++;
                }

                isSucceed = length != failCount;
                content = $"添加torrent任务完成 （{length}个）" + (failCount > 0 ? "，失败数:{failCount}" : string.Empty);
                break;
            }
        }

        // 有成功项的话，添加打开目录的按钮
        if (isSucceed)
        {
            ShowTeachingTip(content, "打开所在目录", (_, _) =>
            {
                Debug.WriteLine("请求打开所在目录");

                // 打开所在目录
                CommonWindow.CreateAndShowWindow(new FileListPage(downPath.FileId));
            });
        }
        else
        {
            ShowTeachingTip(content);
        }
    }

    private void ShowTeachingTip(string subtitle, string content = null)
    {
        BasePage.ShowTeachingTip(LightDismissTeachingTip, subtitle, content);
    }

    private void ShowTeachingTip(string subtitle,
        string actionContent, TypedEventHandler<TeachingTip, object> actionButtonClick)
    {
        BasePage.ShowTeachingTip(LightDismissTeachingTip, subtitle, actionContent, actionButtonClick);
    }

}

public class FailLoadedEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

public class RequestCompletedEventArgs(AddTaskUrlInfo result) : EventArgs
{
    public AddTaskUrlInfo Info { get; } = result;
}