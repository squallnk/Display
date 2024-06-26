﻿using Microsoft.UI.Xaml;
using SharpCompress;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using DataAccess.Dao.Interface;
using DataAccess.Models.Entity;
using Display.Models.Api.EditorCookie;
using Display.Models.Vo;
using Display.Providers;

namespace Display.Helper.FileProperties.Name;

public static class FileMatch
{
    private static readonly IFilesInfoDao FilesInfoDao = App.GetService<IFilesInfoDao>();

    private static readonly IFileToInfoDao FileToInfoDao = App.GetService<IFileToInfoDao>();
    
    /// <summary>
    /// 正则删除某些关键词
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    private static string DeleteSomeKeywords(string name)
    {
        var regReplaceList =
            new List<string> { "uur76", @"({\d}K)?\d{2,3}fps\d{0,}", @"part\d", "@18P2P", @"[^\d]\d{3,6}P", @"\[?[0-9a-z]+?[\._](com|cn|xyz|la|me|net|app|cc)\]?@?",
                                @"SE\d{2}",@"EP\d{2}", @"S\d{1,2}E\d{1,2}", @"\D[hx]26[54]", "[-_][468]k", "h_[0-9]{3,4}",@"[a-z0-9]{15,}",
                                @"\d+bit",@"\d{3,6}x\d{3,6}", @"whole[-_\w]+$"};

        return regReplaceList.Select(t => new Regex(t, RegexOptions.IgnoreCase)).Aggregate(name, (current, rgx) => rgx.Replace(current, ""));
    }

    /// <summary>
    /// 从文件名中匹配CID名字
    /// </summary>
    /// <param name="srcText"></param>
    /// <param name="fileCid">父级目录的id，通过数据库获取id对应的目录名</param>
    /// <returns></returns>
    public static string MatchName(string srcText, long? fileCid = null)
    {
        //提取文件名
        var name = Regex.Replace(srcText, @"(\.\w{3,5}?)$", "");

        //删除空格
        name = name.Replace(" ", "_");

        //转大写
        var nameUp = name.ToUpper();

        Match match;
        var noDomain = nameUp;
        if (nameUp.Contains("FC"))
        {
            //根据FC2 Club的影片数据，FC2编号为5-7个数字
            match = Regex.Match(nameUp, @"FC2?[^A-Z\d]{0,5}(PPV[^A-Z\d]{0,5})?(\d{5,7})");

            if (match.Success)
            {
                return $"FC2-{match.Groups[2].Value}";
            }
        }
        else if (nameUp.Contains("HEYDOUGA"))
        {
            match = Regex.Match(nameUp, @"(HEYDOUGA)[-_]*(\d{4})[-_]+0?(\d{3,5})");

            if (match.Success)
            {
                return string.Join("-", match.Groups.Values.Skip(1));
            }
        }
        else
        {
            //先尝试移除可疑关键词进行匹配，如果匹配不到再使用去掉关键词的名称进行匹配
            noDomain = DeleteSomeKeywords(noDomain);

            if (!string.IsNullOrEmpty(noDomain) && noDomain != nameUp)
            {
                return MatchName(noDomain);
            }
        }

        if (noDomain == null) return string.Empty;

        //匹配缩写成hey的heydouga影片。由于番号分三部分，要先于后面分两部分的进行匹配
        match = Regex.Match(noDomain, @"(?:HEY)[-_]*(\d{4})[-_]+0?(\d{3,5})");
        if (match.Success)
        {
            return "HEYDOUGA-" + string.Join("-", match.Groups.Values.Skip(1));
        }
        //普通番号，优先尝试匹配带分隔符的（如ABC - 123）
        match = Regex.Match(noDomain, @"([A-Z]{2,10})[-_]+0*(\d{2,5})");
        if (match.Success)
        {
            var number = match.Groups[2].Value;
            //不满三位数，填充0
            number = number.PadLeft(3, '0');

            return $"{match.Groups[1].Value}-{number}";
        }

        //然后再将影片视作缺失了 - 分隔符来匹配
        match = Regex.Match(noDomain, @"([A-Z]{2,})0*(\d{2,5})");
        if (match.Success)
        {
            var number = match.Groups[2].Value;
            //不满三位数，填充0
            number = number.PadLeft(3, '0');

            return $"{match.Groups[1].Value}-{number}";
        }

        //普通番号，运行到这里时表明无法匹配到带分隔符的番号
        //先尝试匹配东热的red, sky, ex三个不带 - 分隔符的系列
        //（这三个系列已停止更新，因此根据其作品编号将数字范围限制得小一些以降低误匹配概率）
        match = Regex.Match(noDomain, @"(RED[01]\d\d|SKY[0-3]\d\d|EX00[01]\d)");
        if (match.Success)
        {
            var matchName = match.Groups[1].Value;
            match = Regex.Match(matchName, @"([A-Z]+)(\d+)");
            return match.Success ? $"{match.Groups[1].Value}-{match.Groups[2].Value}" : matchName;
        }

        //尝试匹配TMA制作的影片（如'T28-557'，他家的番号很乱）
        match = Regex.Match(noDomain, @"(T28[-_]+\d{3})");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        //尝试匹配东热n, k系列
        match = Regex.Match(noDomain, @"(N\d{4}|K\d{4})");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        //尝试匹配纯数字番号（无码影片）
        match = Regex.Match(noDomain, @"(\d{6}[-_]+\d{2,3})");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        //如果还是匹配不了，尝试将')('替换为'-'后再试，少部分影片的番号是由')('分隔的
        if (noDomain.Contains(")("))
        {
            var avid = MatchName(noDomain.Replace(")(", "-"));
            if (!string.IsNullOrEmpty(avid))
                return avid;
        }

        //如果最后仍然匹配不了番号，则尝试使用文件所在文件夹的名字去匹配
        if (fileCid == null) return null;
        var folderDatum = FilesInfoDao.GetUpperLevelFolderInfoByFolderId((long)fileCid);

        return !string.IsNullOrEmpty(folderDatum?.Name) ? MatchName(folderDatum.Name) : null;
    }

    public static bool IsLike(int isLike)
    {
        return isLike != 0;
    }

    public static bool IsLookLater(long? lookLater)
    {
        return lookLater != null && lookLater != 0;
    }

    //是否显示喜欢图标
    public static Visibility IsShowLikeIcon(int isLike)
    {
        return isLike == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    //根据类别搜索结果
    public static async Task<List<VideoInfo>> GetVideoInfoFromType(List<string> types, string keywords, int limit)
    {
        if (types == null) return null;

        //避免重复
        Dictionary<string, VideoInfo> dictionary = new();

        foreach (var type in types)
        {
            string trueType;
            switch (type)
            {
                case "番号" or "truename":
                    trueType = "truename";
                    break;
                case "演员" or "actor":
                    trueType = "actor";
                    break;
                case "标签" or "category":
                    trueType = "category";
                    break;
                case "标题" or "title":
                    trueType = "title";
                    break;
                case "片商" or "producer":
                    trueType = "producer";
                    break;
                case "导演" or "director":
                    trueType = "director";
                    break;
                //失败比较特殊
                //从另外的表中查找
                case "失败" or "fail":
                    var failItems = await DataAccessLocal.Get.GetFailFileInfoWithFilesInfoAsync(n: keywords, limit: limit);
                    failItems?.ForEach(item => dictionary.TryAdd(item.Name, new FailVideoInfo(item)));
                    continue;
                default:
                    trueType = "truename";
                    break;
            }

            var leftCount = limit - dictionary.Count;

            // 当数量超过Limit数量时，跳过（不包括失败列表）
            if (leftCount <= 0) continue;

            var newItems = DataAccessLocal.Get.GetVideoInfoBySomeType(trueType, keywords, leftCount);

            newItems?.ForEach(item => dictionary.TryAdd(item.TrueName, item));
        }

        return dictionary.Values.ToList();
    }

    public static string GetVideoPlayUrl(string pickCode)
    {
        return $"https://v.anxia.com/?pickcode={pickCode}&share_id=0";
    }

    /// <summary>
    /// 从文件中挑选出视频文件
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static List<MatchVideoResult> GetVideoAndMatchFile(List<FilesInfo> data)
    {
        //根据视频信息匹配视频文件
        List<MatchVideoResult> resultList = [];

        foreach (var fileInfo in data)
        {
            var fileName = fileInfo.Name;

            //挑选视频文件
            if (fileInfo.Iv == 1)
            {
                //根据视频名称匹配番号
                var videoName = MatchName(fileName, fileInfo.CurrentId);

                //未匹配
                if (videoName == null)
                {
                    resultList.Add(new MatchVideoResult { Status = false, OriginalName = fileInfo.Name, StatusCode = -1, Message = "匹配失败" });
                }
                //匹配后，查询是否重复匹配
                else
                {
                    var existsResult = resultList.FirstOrDefault(x => x.MatchName == videoName);

                    resultList.Add(existsResult == null
                        ? new MatchVideoResult
                        {
                            Status = true,
                            OriginalName = fileInfo.Name,
                            Message = "匹配成功",
                            StatusCode = 1,
                            MatchName = videoName
                        }
                        : new MatchVideoResult { Status = true, OriginalName = fileInfo.Name, StatusCode = 2, Message = "已添加" });
                }

                // 添加到数据库
                FileToInfoDao.ExecuteInitIfNoExists(new FileToInfo
                {
                    FilePickCode = fileInfo.PickCode,
                    TrueName = videoName,
                    IsSuccess = 0
                });
            }
            else
            {
                resultList.Add(new MatchVideoResult { Status = true, OriginalName = fileInfo.Name, StatusCode = 0, Message = "跳过非视频" });
            }
        }

        return resultList;
    }

    public static List<CookieFormat> ExportCookies(string cookie)
    {
        List<CookieFormat> cookieList = [];

        var cookiesList = cookie.Split(';');
        foreach (var cookies in cookiesList)
        {
            var item = cookies.Split('=');

            if (item.Length != 2)
                continue;

            var key = item[0].Trim();
            var value = item[1].Trim();
            switch (key)
            {
                case "acw_tc":
                    cookieList.Add(new CookieFormat { Name = key, Value = value, Domain = "115.com", HostOnly = true });
                    break;
                case "115_lang":
                    cookieList.Add(new CookieFormat { Name = key, Value = value, HttpOnly = false });
                    break;
                case "CID" or "SEID" or "UID" or "USERSESSIONID":
                    cookieList.Add(new CookieFormat { Name = key, Value = value });
                    break;
                //mini_act……_dialog_show
                default:
                    cookieList.Add(new CookieFormat { Name = key, Value = value, Session = true });
                    break;
            }
        }
        return cookieList;
    }

    public static async void LaunchFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var folder = await StorageFolder.GetFolderFromPathAsync(path);
        await Launcher.LaunchFolderAsync(folder);
    }

    public static async Task<StorageFolder> OpenFolder(object target, PickerLocationId suggestedStartLocation)
    {
        FolderPicker folderPicker = new();
        folderPicker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(target);
        folderPicker.SuggestedStartLocation = suggestedStartLocation;

        WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

        return await folderPicker.PickSingleFolderAsync();
    }

    public static void OpenFolderWithSystemExplorer(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;

        LaunchFolder(folder);
    }

    public static async Task<StorageFile> SelectFileAsync(Window window, IList<string> fileTypeFilters = null)
    {
        FileOpenPicker fileOpenPicker = new();
        fileTypeFilters?.ForEach(fileOpenPicker.FileTypeFilter.Add);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        fileOpenPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        WinRT.Interop.InitializeWithWindow.Initialize(fileOpenPicker, hwnd);

        return await fileOpenPicker.PickSingleFileAsync();
    }

    public static void CreateDirectoryIfNotExists(string savePath)
    {
        if (string.IsNullOrEmpty(savePath)) return;

        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
    }

    //临时方法
    public static Visibility ShowIfImageNotNull(string imagePath)
    {
        return imagePath == Constants.FileType.NoPicturePath ? Visibility.Collapsed : Visibility.Visible;

    }

    //临时方法
    public static Visibility ShowIfImageNull(string imagePath)
    {
        return imagePath == Constants.FileType.NoPicturePath ? Visibility.Visible : Visibility.Collapsed;
    }

    public static IEnumerable<T> OrderByNatural<T>(this IEnumerable<T> items, Func<T, string> selector, StringComparer stringComparer = null)
    {
        var regex = new Regex(@"\d+", RegexOptions.Compiled);

        var enumerable = items.ToList();
        var maxDigits = enumerable
                      .SelectMany(i => regex.Matches(selector(i)).Select(digitChunk => (int?)digitChunk.Value.Length))
                      .Max() ?? 0;

        return enumerable.OrderBy(i => regex.Replace(selector(i), match => match.Value.PadLeft(maxDigits, '0')), stringComparer ?? StringComparer.CurrentCulture);
    }

    public static Tuple<string, string> SplitLeftAndRightFromCid(string cid)
    {
        var splitList = cid.Split('-', '_');
        var leftName = splitList[0];

        var rightNumber = string.Empty;
        if (splitList.Length != 1)
        {
            rightNumber = splitList[1];
        }
        else
        {
            //SE221
            var result = Regex.Match(leftName, "^([a-z]+)([0-9]+)$", RegexOptions.IgnoreCase);
            if (result.Success)
            {
                leftName = result.Groups[1].Value;
                rightNumber = result.Groups[2].Value;
            }
        }

        return Tuple.Create(leftName, rightNumber);
    }
}
