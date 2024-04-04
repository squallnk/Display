﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Display.Models.Dto.OneOneFive;
using Display.Providers;
using Microsoft.UI.Xaml.Data;
using SharpCompress;

namespace Display.Models.Vo.IncrementalCollection;

public class IncrementalLoadSuccessInfoCollection : ObservableCollection<VideoInfoVo>, ISupportIncrementalLoading
{
    private const int DefaultCount = 30;
    private double ImageWidth { get; set; }
    private double ImageHeight { get; set; }
    private bool IsFuzzyQueryActor { get; set; }

    private bool IsContainFail => FilterConditionList != null && FilterConditionList.Contains("fail");

    private string OrderBy { get; set; }
    private bool IsDesc { get; set; }

    private Dictionary<string, string> Ranges { get; set; }

    private List<string> FilterConditionList { get; set; }

    private string FilterKeywords { get; set; }


    public int AllCount { get; private set; }

    public bool HasMoreItems { get; set; } = true;

    public IncrementalLoadSuccessInfoCollection()
    {

    }

    public IncrementalLoadSuccessInfoCollection(double imgWidth, double imgHeight)
    {
        SetImageSize(imgWidth, imgHeight);
    }

    public Task LoadData(int startShowCount = 20)
    {
        Clear();
        var newItems = DataAccessLocal.Get.GetVideoInfo(startShowCount, 0, OrderBy, IsDesc, FilterConditionList, FilterKeywords, Ranges, IsFuzzyQueryActor);

        var successCount = DataAccessLocal.Get.GetCountOfVideoInfo(FilterConditionList, FilterKeywords, Ranges);
        var failCount = 0;
        if (IsContainFail)
        {
            failCount = DataAccessLocal.Get.GetCountOfFailFileInfoWithFilesInfo(0, -1, FilterKeywords);
        }

        AllCount = successCount + failCount;

        newItems?.ForEach(item => Add(new VideoInfoVo(item, ImageWidth, ImageHeight)));
        return Task.CompletedTask;
    }

    public void SetImageSize(double imgWidth, double imgHeight)
    {
        ImageWidth = imgWidth;
        ImageHeight = imgHeight;
    }

    public void SetFilter(List<string> filterConditionList, string filterKeywords, bool isFuzzyQueryActor)
    {
        this.FilterConditionList = filterConditionList;
        this.FilterKeywords = filterKeywords;
        this.IsFuzzyQueryActor = isFuzzyQueryActor;
        HasMoreItems = true;
    }

    public void SetRange(Dictionary<string, string> ranges)
    {
        Ranges = ranges;
    }

    public void SetOrder(string orderBy, bool isDesc)
    {
        OrderBy = orderBy;
        IsDesc = isDesc;
    }

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return InnerLoadMoreItemsAsync().AsAsyncOperation();
    }


    private async Task<LoadMoreItemsResult> InnerLoadMoreItemsAsync()
    {
        var lists = DataAccessLocal.Get.GetVideoInfo(DefaultCount, Count, OrderBy, IsDesc, FilterConditionList, FilterKeywords, Ranges, IsFuzzyQueryActor);

        //在最后的时候加载匹配失败的
        //用于展示搜索结果
        if (lists == null || lists.Length < DefaultCount)
        {
            HasMoreItems = false;

            //最后显示匹配失败，如果需要显示的话
            //无筛选功能
            if (IsContainFail)
            {
                var failList = DataAccessLocal.Get.GetFailFileInfoWithFilesInfo(0, -1, FilterKeywords);
                
                foreach (var filesInfo in failList)
                {
                    var failVideoInfo = new FailVideoInfo(filesInfo);
                    // var videoInfo = new FailVideoInfo(failVideoInfo, ImageWidth, ImageHeight);
                    Add(failVideoInfo);
                }
                
            }
        }

        lists?.ForEach(item => Add(new VideoInfoVo(item, ImageWidth, ImageHeight)));

        return new LoadMoreItemsResult
        {
            Count = (uint)(lists?.Length ?? 0)
        };
    }
}
