﻿
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Display.Data;
using SharpCompress;

namespace Display.Models;

public class IncrementallLoadActorInfoCollection : ObservableCollection<ActorInfo>, ISupportIncrementalLoading
{
    public bool HasMoreItems { get; set; } = true;

    public int AllCount { get; private set; }

    public Dictionary<string, bool> orderByList { get; private set; }

    public List<string> filterList { get; private set; }

    public bool isDsc { get; private set; }

    public IncrementallLoadActorInfoCollection(Dictionary<string, bool> orderByList, int defaultAddCount = 40)
    {
        this._defaultAddCount = defaultAddCount;

        SetOrder(orderByList);
    }

    public async Task<int> LoadData(int limit = 40, int offset = 0)
    {
        System.Diagnostics.Debug.WriteLine($"加载{offset}-{offset + limit} 中……");

        var ActorInfos = await DataAccess.Get.GetActorInfo(limit, offset, orderByList, filterList);

        if (Count == 0)
        {
            this.AllCount = DataAccess.Get.GetCountOfActorInfo(filterList);
            System.Diagnostics.Debug.WriteLine($"总数量:{this.AllCount}");

            System.Diagnostics.Debug.WriteLine($"HasMoreItems:{HasMoreItems}");
        }

        ActorInfos.ForEach(Add);

        if (AllCount <= Count)
        {
            HasMoreItems = false;
            System.Diagnostics.Debug.WriteLine("记载完毕");
        }

        return ActorInfos.Length;
    }

    public void SetFilter(List<string> filterList)
    {
        this.filterList = filterList;
        Clear();
        if (!HasMoreItems) HasMoreItems = true;
    }

    public void SetOrder(Dictionary<string, bool> orderBy)
    {
        this.orderByList = orderBy;
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return InnerLoadMoreItemsAsync(count).AsAsyncOperation();
    }

    private readonly int _defaultAddCount = 30;
    private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync(uint count)
    {
        int getCount = await LoadData(_defaultAddCount, Count);


        return new LoadMoreItemsResult
        {
            Count = (uint)getCount
        };
    }
}

