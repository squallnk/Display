﻿using Display.Data;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Display.Extensions;

namespace Display.Models.IncrementalCollection;

public class IncrementalLoadDatumCollection : ObservableCollection<FilesInfo>, ISupportIncrementalLoading
{
    private WebApi WebApi { get; set; }

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

    public new void Insert(int index, FilesInfo item)
    {
        base.Insert(index, item);
        AllCount++;

        if (IsNull) IsNull = false;
    }

    public new void Remove(FilesInfo item)
    {
        base.Remove(item);
        AllCount--;

        if (AllCount == 0) IsNull = true;
    }

    public WebApi.OrderBy OrderBy { get; private set; } = WebApi.OrderBy.UserProduceTime;

    public int Asc { get; private set; }

    public long Cid { get; set; }

    private bool IsOnlyFolder { get; set; }

    public IncrementalLoadDatumCollection(long cid, bool isOnlyFolder = false)
    {
        Cid = cid;
        WebApi = WebApi.GlobalWebApi;
        IsOnlyFolder = isOnlyFolder;
    }

    public WebPath[] WebPaths;

    public async Task<WebFileInfo> GetFilesInfoAsync(int limit, int offset)
    {
        //查询该目录的排列方式
        var filesShowMethod = await WebApi.GetFilesShowInfo(Cid);

        //Enum.TryParse(filesShowMethod.order, out WebApi.OrderBy order);
        var order = filesShowMethod.order.ParseEnumByDescription<WebApi.OrderBy>();

        if (OrderBy != order) OrderBy = order;

        Asc = filesShowMethod.is_asc;

        var filesInfo = await WebApi.GetFileAsync(Cid, limit, offset, orderBy: OrderBy, asc: Asc, isOnlyFolder: IsOnlyFolder);
        WebPaths = filesInfo.path;
        AllCount = filesInfo.count;

        //汇报事件
        GetFileInfoCompletedEventArgs args = new() { Orderby = OrderBy, Asc = Asc, TimeReached = DateTime.Now };
        GetFileInfoCompleted?.Invoke(this, args);

        return filesInfo;
    }

    public async Task<int> LoadData(int limit = 40, int offset = 0)
    {
        IsNull = false;
        var filesInfo = await GetFilesInfoAsync(limit, offset);

        // 访问失败，发生未知错误
        if (filesInfo.data == null)
        {
            throw new ArgumentException(nameof(filesInfo.data));
            //HasMoreItems = false;
            //return 0;
        }

        if (AllCount == 0)
        {
            IsNull = true;
            HasMoreItems = false;
            return 0;
        }

        foreach (var datum in filesInfo.data)
        {
            Add(new FilesInfo(datum));
        }

        HasMoreItems = AllCount > Count;



        return filesInfo.data.Length;
    }

    public async Task SetCid(long cid)
    {
        Cid = cid;
        Clear();

        //当目录为空时，HasMoreItems==True无法激活查询服务，需要手动LoadData
        var isNeedLoad = Count == 0;
        HasMoreItems = !isNeedLoad;

        if (isNeedLoad) await LoadData();
    }

    public async Task SetOrder(WebApi.OrderBy orderBy, int asc)
    {
        OrderBy = orderBy;
        Asc = asc;
        await WebApi.ChangedShowType(Cid, orderBy, asc);
        await SetCid(Cid);
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
    }

    private readonly int _defaultCount = 30;
    private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
    {
        var getCount = HasMoreItems ? await LoadData(_defaultCount, Count) : 0;

        return new LoadMoreItemsResult
        {
            Count = (uint)getCount
        };
    }

    public event EventHandler<GetFileInfoCompletedEventArgs> GetFileInfoCompleted;
}

public class GetFileInfoCompletedEventArgs : EventArgs
{
    public WebApi.OrderBy Orderby { get; set; }

    public int Asc { get; set; }

    public DateTime TimeReached { get; set; }
}