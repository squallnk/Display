﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Foundation;
using Display.Extensions;
using Display.Models.Api.OneOneFive.File;
using Display.Models.Vo.OneOneFive;
using Display.Providers;
using Microsoft.UI.Xaml.Data;
using SharpCompress;

namespace Display.Models.Vo.IncrementalCollection;

internal class IncrementalLoadDatumCollection(long cid, bool isOnlyFolder = false)
    : ObservableCollection<DetailFileInfo>, ISupportIncrementalLoading
{
    private WebApi WebApi { get; } = WebApi.GlobalWebApi;

    private bool _isBusy;

    public bool HasMoreItems { get; private set; } = true;

    private int _allCount;
    public int AllCount
    {
        get => _allCount;
        private set
        {
            if (_allCount == value) return;

            _allCount = value;

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(AllCount)));
        }
    }

    private bool _isNull;

    public bool IsNull
    {
        get => _isNull;
        set
        {
            if (_isNull == value) return;

            _isNull = value;

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsNull)));
        }
    }

    public new void Insert(int index, DetailFileInfo item)
    {
        base.Insert(index, item);
        AllCount++;

        if (IsNull) IsNull = false;
    }

    public void AddArray(DetailFileInfo[] items)
    {
        items.ForEach(Add);
        AllCount += items.Length;

        if (IsNull) IsNull = false;
    }

    public new void Remove(DetailFileInfo item)
    {
        base.Remove(item);
        AllCount--;

        if (AllCount == 0) IsNull = true;
    }

    private WebApi.OrderBy OrderBy { get; set; } = WebApi.OrderBy.UserProduceTime;

    private int Asc { get; set; }

    public long Cid { get; private set; } = cid;

    private bool IsOnlyFolder { get; } = isOnlyFolder;


    private WebPath[] _webPaths;

    public WebPath[] WebPaths
    {
        get => _webPaths;
        private set
        {
            if (_webPaths == value) return;

            _webPaths = value;

            WebPathChanged?.Invoke(_webPaths);
        }
    }

    private async Task<WebFileInfo> GetFilesInfoAsync(int limit, int offset)
    {
        Debug.WriteLine($"从网络中获取文件列表:{offset}-{limit + offset}");
        var filesInfo = await WebApi.GetFileAsync(Cid, limit, offset, orderBy: OrderBy.GetDescription(), asc: Asc, isOnlyFolder: IsOnlyFolder);
        if (filesInfo == null) return null;

        WebPaths = filesInfo.Path;
        AllCount = filesInfo.Count;

        //查询该目录的排列方式
        var order = filesInfo.Order.ParseEnumByDescription<WebApi.OrderBy>();
        if (OrderBy != order) OrderBy = order;
        Asc = filesInfo.IsAsc;

        //汇报事件
        GetFileInfoCompletedEventArgs args = new() { OrderBy = OrderBy, Asc = Asc, TimeReached = DateTime.Now };
        GetFileInfoCompleted?.Invoke(this, args);

        return filesInfo;
    }

    private async Task<int> LoadData(int limit = 40, int offset = 0)
    {
        IsNull = false;

        var filesInfo = await GetFilesInfoAsync(limit, offset);

        // 访问失败，没有登陆
        // 或者没有数据
        if (filesInfo?.Data == null || AllCount == 0)
        {
            IsNull = true;
            HasMoreItems = false;
            return 0;
        }

        filesInfo.Data.ForEach(i => Add(new DetailFileInfo(i)));

        HasMoreItems = AllCount > Count;
        Debug.WriteLine($"[{Count}/{AllCount}] HasMoreItems = {HasMoreItems}");

        return filesInfo.Data.Length;
    }

    public async Task SetCid(long cid)
    {
        if (_isBusy) return;

        // 两个进程同时执行Clear操作会闪退
        _isBusy = true;

        Cid = cid;

        // 避免使用LoadMoreItemsAsync重复加载数据，LoadData后会重新设置为正确的值
        HasMoreItems = false;

        Clear();

        //当目录为空时，HasMoreItems==True无法激活查询服务，需要手动LoadData
        //var isNeedLoad = Count == 0;
        //HasMoreItems = true;
        //HasMoreItems = !isNeedLoad;

        await LoadData();

        _isBusy = false;
    }

    public async Task SetOrder(WebApi.OrderBy orderBy, int asc)
    {
        OrderBy = orderBy;
        Asc = asc;
        await WebApi.ChangedShowType(Cid, orderBy, asc);
        await SetCid(Cid);
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint _)
    {
        return InnerLoadMoreItemsAsync().AsAsyncOperation();
    }

    private readonly int _defaultCount = 30;
    private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync()
    {
        var getCount = HasMoreItems ? await LoadData(_defaultCount, Count) : 0;

        return new LoadMoreItemsResult
        {
            Count = (uint)getCount
        };
    }

    public event EventHandler<GetFileInfoCompletedEventArgs> GetFileInfoCompleted;
    public event Action<WebPath[]> WebPathChanged;
}

internal class GetFileInfoCompletedEventArgs : EventArgs
{
    public WebApi.OrderBy OrderBy { get; init; }

    public int Asc { get; init; }

    public DateTime TimeReached { get; set; }
}
