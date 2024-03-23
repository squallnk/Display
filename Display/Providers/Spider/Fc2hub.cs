﻿using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpiderInfo = Display.Models.Spider.SpiderInfos;
using Display.Helper.Network;
using Display.Helper.Date;
using Display.Models.Data;
using Display.Models.Spider;

namespace Display.Providers.Spider;

public class Fc2hub
{
    public const int Id = (int)SpiderInfo.SpiderSourceName.Fc2club;

    public const string Abbreviation = "fc";

    public const string Keywords = "Fc2hub.com";

    public static Tuple<int, int> DelayRanges = new(1, 2);

    public const bool OnlyFc2 = true;

    public const bool IgnoreFc2 = false;

    public static bool IsOn => AppSettings.IsUseFc2Hub;

    private static string baseUrl => AppSettings.Fc2HubBaseUrl;

    public static async Task<VideoInfo> SearchInfoFromCID(string CID, CancellationToken token)
    {
        string url = GetInfoFromNetwork.UrlCombine(baseUrl, $"search?kw={CID.Replace("FC2-", "")}");

        Tuple<string, string> result = await RequestHelper.RequestHtml(Common.Client, url, token);
        if (result == null) return null;

        string detail_url = result.Item1;
        string htmlString = result.Item2;

        HtmlDocument htmlDoc = new HtmlDocument();
        htmlDoc.LoadHtml(htmlString);

        return await GetVideoInfoFromHtmlDoc(CID, detail_url, htmlDoc);
    }

    public static async Task<VideoInfo> GetVideoInfoFromHtmlDoc(string CID, string detail_url, HtmlDocument htmlDoc)
    {
        VideoInfo videoInfo = new VideoInfo();
        videoInfo.busUrl = detail_url;
        //默认是步兵
        videoInfo.IsWm = 1;

        var jsons = htmlDoc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");

        if (jsons == null || jsons.Count == 0) return null;

        var jsonString = jsons.Last().InnerText;

        var json = JsonConvert.DeserializeObject<FcJson>(jsonString);

        if (json.name == null || json.image == null) return null;

        videoInfo.Title = json.name;
        videoInfo.trueName = CID;
        videoInfo.ReleaseTime = json.datePublished.Replace("/", "-");
        //PTxHxMxS转x分钟
        videoInfo.Lengthtime = DateHelper.ConvertPtTimeToTotalMinute(json.duration);
        videoInfo.Director = json.director;
        videoInfo.Producer = "fc2";

        if (json.genre != null)
            videoInfo.Category = string.Join(",", json.genre);

        if (json.actor != null)
            videoInfo.Actor = string.Join(",", json.actor);


        string ImageUrl = string.Empty;
        if (json.image != null)
        {
            ImageUrl = json.image;
        }
        else
        {
            var imageNode = htmlDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");

            if (imageNode != null)
            {
                ImageUrl = imageNode.GetAttributeValue("content", string.Empty);
            }
        }

        ////下载封面
        if (!string.IsNullOrEmpty(ImageUrl))
        {
            string SavePath = AppSettings.ImageSavePath;
            string filePath = Path.Combine(SavePath, CID);
            videoInfo.ImageUrl = ImageUrl;
            videoInfo.ImagePath = await GetInfoFromNetwork.DownloadFile(ImageUrl, filePath, CID);
        }

        return videoInfo;
    }
}