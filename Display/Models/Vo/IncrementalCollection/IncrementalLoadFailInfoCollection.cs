﻿using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using DataAccess.Models.Entity;
using Display.Models.Enums.OneOneFive;
using Display.Providers;
using Microsoft.UI.Xaml.Data;
using SharpCompress;

namespace Display.Models.Vo.IncrementalCollection;

public class IncrementalLoadFailInfoCollection(FailInfoShowType showType)
    : ObservableCollection<FailListIsLikeLookLater>, ISupportIncrementalLoading
{
    public bool HasMoreItems { get; set; } = true;

    public int AllCount { get; private set; }

    public FailInfoShowType ShowType { get; private set; } = showType;

    public void SetShowType(FailInfoShowType showType)
    {
        ShowType = showType;

        if (!HasMoreItems) HasMoreItems = true;

        Clear();

        LoadData();
    }

    private int LoadData(int limit = 20, int offset = 0)
    {
        var infos = DataAccessLocal.Get.GetFailFileInfoWithFailInfo(offset, limit, ShowType);

        infos?.ForEach(Add);

        var getCount = infos?.Length ?? 0;

        if (Count == 0)
            AllCount = DataAccessLocal.Get.GetCountOfFailInfos(ShowType);

        if (AllCount <= Count || getCount == 0)
        {
            HasMoreItems = false;
        }

        return getCount;
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return InnerLoadMoreItemsAsync().AsAsyncOperation();
    }

    private readonly int _defaultCount = 20;
    private Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync()
    {
        var getCount = LoadData(_defaultCount, Count);

        return Task.FromResult(new LoadMoreItemsResult
        {
            Count = (uint)getCount
        });
    }
}
