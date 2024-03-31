﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Display.Models.Dto.OneOneFive;
using Display.Models.Entities.OneOneFive;
using Display.Providers;
using Microsoft.UI.Xaml.Data;
using SharpCompress;

namespace Display.Models.Data.IncrementalCollection;

public class IncrementalLoadActorInfoCollection : ObservableCollection<ActorInfo>, ISupportIncrementalLoading
{
    public bool HasMoreItems { get; set; } = true;

    public int AllCount { get; private set; }

    private Dictionary<string, bool> OrderByList { get; set; }

    private List<string> FilterList { get; set; }

    public IncrementalLoadActorInfoCollection(Dictionary<string, bool> orderByList, int defaultAddCount = 40)
    {
        _defaultAddCount = defaultAddCount;

        SetOrder(orderByList);
    }

    public async Task<int> LoadData(int limit = 40, int offset = 0)
    {
        System.Diagnostics.Debug.WriteLine($"加载{offset}-{offset + limit} 中……");

        var actorInfos = await DataAccess.Get.GetActorInfo(limit, offset, OrderByList, FilterList);
        if (actorInfos == null)
        {
            HasMoreItems = false;
            return 0;
        }

        if (Count == 0)
        {
            AllCount = DataAccess.Get.GetCountOfActorInfo(FilterList);
            System.Diagnostics.Debug.WriteLine($"总数量:{AllCount}");

            System.Diagnostics.Debug.WriteLine($"HasMoreItems:{HasMoreItems}");
        }

        actorInfos.ForEach(Add);

        if (AllCount > Count) return actorInfos.Length;

        HasMoreItems = false;
        System.Diagnostics.Debug.WriteLine("记载完毕");

        return actorInfos.Length;
    }

    public void SetFilter(List<string> filterList)
    {
        FilterList = filterList;
        Clear();
        if (!HasMoreItems) HasMoreItems = true;
    }

    private void SetOrder(Dictionary<string, bool> orderBy)
    {
        OrderByList = orderBy;
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return InnerLoadMoreItemsAsync().AsAsyncOperation();
    }

    private readonly int _defaultAddCount;
    private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync()
    {
        var getCount = await LoadData(_defaultAddCount, Count);

        return new LoadMoreItemsResult
        {
            Count = (uint)getCount
        };
    }
}
