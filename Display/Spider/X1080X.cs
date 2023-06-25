﻿using System;
using Display.Data;
using Display.Helper;
using Display.Models;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Devices.Lights;
using Windows.Media.Protection.PlayReady;
using SharpCompress.Readers;
using static System.Int32;
using System.Reflection.PortableExecutable;
using Windows.System.Profile;
using System.Web;

namespace Display.Spider
{
    public class X1080X
    {
        private static string BaseUrl => AppSettings.X1080XBaseUrl;

        public static bool IsOn => AppSettings.IsUseX1080X;

        private static HttpClient _client;

        private static readonly string[] ExpectType = { "4K超清", "高清有碼", "BT綜合區" };

        public static HttpClient Client
        {
            get
            {
                var headers = new Dictionary<string, string>
                {
                    {"user-agent",AppSettings.X1080XUa},
                    {"cookie",AppSettings.X1080XCookie},
                    {"referer",$"{BaseUrl}forum.php"}
                };
                return _client ??= GetInfoFromNetwork.CreateClient(headers);
            }
            set=> _client = value;
        }

        public static void TryChangedClientHeader(string key, string value)
        {
            if(_client == null) return;

            if (_client.DefaultRequestHeaders.Contains(key))
            {
                _client.DefaultRequestHeaders.Remove(key);
            }

            _client.DefaultRequestHeaders.Add(key,value);
        }

        public static async Task<List<Forum1080AttachmentInfo>> GetDownLinkFromUrl(string url)
        {
            var result = await RequestHelper.RequestHtml(Client, url);

            var htmlString = result.Item2;

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlString);

            return GetAttmnInfoFromHtml(htmlDoc);
        }

        private static List<Forum1080AttachmentInfo> GetAttmnInfoFromHtml(HtmlDocument htmlDoc)
        {
            HashSet<Forum1080AttachmentInfo> links = new();

            // 先查找磁力链接
            var codeNodes = htmlDoc.DocumentNode.SelectNodes(".//div[contains(@id,'code')]");
            if (codeNodes != null)
            {
                foreach (var node in codeNodes)
                {
                    var codeContent = node.InnerText;
                    foreach (var line in codeContent.Split('\n'))
                    {
                        links.Add(new Forum1080AttachmentInfo(line, AttmnType.Magnet));
                    }
                }
            }

            if (links.Count > 0)
            {
                return links.ToList();
            }

            //附件
            var attmnNodes = htmlDoc.DocumentNode.SelectNodes(".//dl[@class='tattl']");

            // 出售区附件
            if (attmnNodes == null)
            {
                var attmnSpanNode = htmlDoc.DocumentNode.SelectNodes(".//span[contains(@id,'attach_')]");
                if (attmnSpanNode == null)
                {
                    return links.ToList();
                }

                foreach (var spanNode in attmnSpanNode)
                {
                    var nameNode = spanNode.SelectSingleNode("a");

                    var description = spanNode.SelectSingleNode("em")?.InnerText;

                    if (nameNode != null && description != null)
                    {
                        var name = nameNode.InnerText.Trim();

                        if (name.Contains("protected"))
                        {
                            var emailNode = nameNode.SelectSingleNode("span");
                            if (emailNode != null)
                            {
                                var data = emailNode.GetAttributeValue("data-cfemail", string.Empty);

                                if (!string.IsNullOrEmpty(data))
                                {
                                    name = RequestHelper.DecodeCfEmail(data);
                                }
                            }
                        }

                        var downLink = nameNode.GetAttributeValue("href", string.Empty);
                        var attmnType = GetAttmnTypeFromName(name);
                        Forum1080AttachmentInfo attmnInfo = new(downLink, attmnType)
                        {
                            Name = name
                        };

                        var result = Regex.Match(description, "([. \\w]+)  , 下载次数: (\\d+)(.*, 售价: 点数 (\\d+))?");
                        if (result.Success)
                        {
                            attmnInfo.Size = result.Groups[1].Value;

                            if (TryParse(result.Groups[2].Value, out int count))
                            {
                                attmnInfo.DownCount = count;
                            }

                            if (TryParse(result.Groups[4].Value, out int countResult))
                            {
                                attmnInfo.Expense = countResult;
                            }
                        }


                        links.Add(attmnInfo);
                    }

                }

                return links.ToList();
            }
            
            // 普通附件
            foreach (var node in attmnNodes)
            {
                var attmnDd = node.SelectSingleNode("dd");

                var attmnA = attmnDd.SelectSingleNode("p[@class='attnm']/a");
                
                var downLink = attmnA.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(downLink)) continue;

                downLink = HttpUtility.HtmlDecode(downLink);
                var name = attmnA.InnerText.Trim();

                var attmnType = GetAttmnTypeFromName(name);

                Forum1080AttachmentInfo attmnInfo = new(downLink, attmnType)
                {
                    Name = name
                };

                var attmnPStr = attmnDd.SelectNodes("p").FirstOrDefault(x => x.InnerText.Contains("下载次数"));
                if (attmnPStr != null)
                {
                    var result = Regex.Match(attmnPStr.InnerText, "([. \\w]+)  , 下载次数: (\\d+)(.*, 售价: 点数 (\\d+))?");
                    if (result.Success)
                    {
                        attmnInfo.Size = result.Groups[1].Value;

                        if (TryParse(result.Groups[2].Value, out int count))
                        {
                            attmnInfo.DownCount = count;
                        }

                        if (TryParse(result.Groups[4].Value, out int countResult))
                        {
                            attmnInfo.Expense = countResult;
                        }
                    }
                }

                links.Add(attmnInfo);

            }

            return links.ToList();
        }

        private static AttmnType GetAttmnTypeFromName(string name)
        {
            AttmnType attmnType;
            if (name.Contains(".rar"))
            {
                attmnType = AttmnType.Rar;
            }
            else if (name.Contains(".txt"))
            {
                attmnType = AttmnType.Txt;
            }
            else if (name.Contains(".torrent"))
            {
                attmnType = AttmnType.Torrent;
            }
            else
            {
                attmnType = AttmnType.Unknown;
            }

            return attmnType;
        }


        public static async Task<List<Forum1080SearchResult>> GetMatchInfosFromCid(string cid)
        {
            var txt = cid.Replace('-', '+').Replace(' ', '+');


            var postValues = new Dictionary<string, string>
            {
                //{ "mod", "search"},
                { "formhash", "b930ec86"},
                { "srchtype", "title"},
                { "srchtxt", txt},
                { "mod", "forum"},
                { "searchsubmit", "true"},
            };

            List<Forum1080SearchResult> detailInfos = new();
            var nextPageUrl = $"{BaseUrl}search.php?searchsubmit=yes";

            while (!string.IsNullOrEmpty(nextPageUrl))
            {
                var result = await RequestHelper.PostHtml(Client, nextPageUrl, postValues);
                if (result == null) return null;

                //var detailUrl = result.Item1;
                var htmlString = result.Item2;

                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(htmlString);

                var searchResult = GetDetailInfoFromSearchResult(cid, htmlDoc);
                if(searchResult==null) return null;

                var currentInfos = searchResult.Item1;
                detailInfos.AddRange(currentInfos);

                nextPageUrl = searchResult.Item2;

                // 有下一页
                if (!string.IsNullOrEmpty(nextPageUrl))
                {
                    // 等待1~3秒
                    await GetInfoFromNetwork.RandomTimeDelay(1, 3);
                }
            }

            return detailInfos;
        }

        private static Tuple<List<Forum1080SearchResult>,string> GetDetailInfoFromSearchResult(string cid,HtmlDocument htmlDoc)
        {
            var searchResultNodes = htmlDoc.DocumentNode.SelectNodes("//li[@class='pbw']");

            //搜索无果，退出
            if (searchResultNodes == null)
            {
                var tipNode = htmlDoc.DocumentNode.SelectSingleNode("//title");

                if (!tipNode.InnerText.Contains("提示信息")) return null;

                var messageNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='messagetext']");

                var content = messageNode != null ? messageNode.InnerText.Trim() : tipNode.InnerText;

                Toast.TryToast("x1080x出错", content);

                return null;
            }

            //分割通过正则匹配得到的CID
            var splitResult = Common.SplitCid(cid.ToUpper());
            if (splitResult == null) return null;

            var leftCid = splitResult.Item1 ;
            var rightCid = splitResult.Item2;

            List<Forum1080SearchResult> detailUrlInfos = new();

            for (var i = 0; i < searchResultNodes.Count; i++)
            {
                string searchLeftCid;
                string searchRightCid;

                var pbwNode = searchResultNodes[i];
                var titleNode = pbwNode.SelectSingleNode(".//h3[@class='xs3']/a");
                var title = titleNode.InnerText;
                var upperText = title.ToUpper();

                var matchResult = Regex.Match(upperText, @$"({leftCid}).*?0?(\d+)");
                if (matchResult.Success)
                {
                    searchLeftCid = matchResult.Groups[1].Value;
                    searchRightCid = matchResult.Groups[2].Value;
                }
                else
                    continue;

                if (searchLeftCid != leftCid
                    || (searchRightCid != rightCid
                        && (!TryParse(rightCid, out var currentNum)
                            || !TryParse(searchRightCid, out var searchNum)
                            || !currentNum.Equals(searchNum)))) continue;

                var detailUrl = titleNode.GetAttributeValue("href",null);
                if(detailUrl == null) continue;

                detailUrl = HttpUtility.HtmlDecode(detailUrl);

                detailUrlInfos.Add(GetForum1080FromNode(pbwNode, title,detailUrl));
            }



            string nextPageUrl = string.Empty;
            //是否有下一页
            var nextPageNode = htmlDoc.DocumentNode.SelectSingleNode(".//a[@class='nxt']");
            if (nextPageNode != null)
            {
                nextPageUrl = HttpUtility.HtmlDecode(nextPageNode.GetAttributeValue("href", string.Empty));
            }

            return new Tuple<List<Forum1080SearchResult>, string>(detailUrlInfos, nextPageUrl);
        }

        public static async Task<string> TryDownAttmn(string url, string name = null)
        {
            string localFilename;

            var isAttmn = url.Contains("attachment");
            if (name == null)
            {
                //附件
                if (isAttmn)
                {
                    var matchName = Regex.Match(url.Replace("%3D", ""), "aid=(\\w+)");
                    localFilename = matchName.Success ? $"{matchName.Groups[1].Value}.rar" : $"tmp_{DateTimeOffset.Now.ToUnixTimeSeconds()}.rar";
                }
                //图片……
                else
                {
                    var urlList = url.Split('/');
                    localFilename = $"{urlList[^2]}_{urlList.Last()}";
                }
            }
            else
            {
                // 是否有后缀名，没有的话就添加
                localFilename = name.Contains(".") ? name : $"{name}{Path.GetExtension(url)}";
            }

            var localPath = Path.Combine(AppSettings.X1080XAttmnSavePath, localFilename);

            if (File.Exists(localPath)) return localPath;

            try
            {
                var responseMessage = await Client.GetAsync(url);

                var isDown = false;

                if (responseMessage.IsSuccessStatusCode)
                {
                    var contentType = responseMessage.Content.Headers.ContentType;
                    
                    //类型为text / html，可能有错误提示，也可能是附件（RAR）
                    if (contentType?.MediaType == "text/html")
                    {
                        var contentDisposition = responseMessage.Content.Headers.ContentDisposition;

                        //为空就无法简单的判断了
                        //通过读取文件判断类型
                        if (contentDisposition == null)
                        {
                            // rar文件前缀: RAR!
                            var byteItems = "Rar!"u8.ToArray();
                            await using var stream = await responseMessage.Content.ReadAsStreamAsync();
                            var bufferSize = 4;
                            var buffer = new byte[bufferSize];
                            var _ = await stream.ReadAsync(buffer, 0, bufferSize);

                            stream.Seek(0, SeekOrigin.Begin);

                            //不是rar前缀，认定为html，输出InnerText结果
                            if (!CheckEquality(buffer, byteItems))
                            {
                                var reader = new StreamReader(stream);
                                var htmlContent = await reader.ReadToEndAsync();
                                //显示提示
                                Debug.WriteLine(htmlContent);
                                return null;
                            }

                            //是rar
                            await using var newStream = File.Create(localPath);
                            await stream.CopyToAsync(newStream);

                            return localPath;
                        }

                        //附件
                        if (contentDisposition.DispositionType == "attachment")
                        {
                            isDown = true;
                        }
                        //错误提示
                        else
                        {
                            var htmlContent = await responseMessage.Content.ReadAsStringAsync();
                            Debug.WriteLine(htmlContent);
                            return null;
                        }
                    }
                    //其他格式文件
                    else
                    {
                        isDown = true;
                    }
                }

                if (isDown)
                {
                    var bytes = await responseMessage.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(localPath, bytes);

                    //await using var stream = await responseMessage.Content.ReadAsStreamAsync();
                    //await using var newStream = File.Create(localPath);
                    //await stream.CopyToAsync(newStream);
                }
                else
                    return null;

                return localPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("下载附件出错：", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 检查两者是否相等
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static bool CheckEquality<T>(T[] first, T[] second)
        {
            return first.SequenceEqual(second);
        }

        private static Forum1080SearchResult GetForum1080FromNode(HtmlNode node,string title,string detailUrl)
        {
            Forum1080SearchResult forum1080SearchResult = new()
            {
                Title = title,
                Url = detailUrl
            };
            
            var ps = node.SelectNodes("p");

            if (ps.Count != 3) return forum1080SearchResult;

            forum1080SearchResult.Description = ps[1].InnerText;

            var span = ps[2].SelectNodes("span");

            if(span.Count != 3)return forum1080SearchResult;

            forum1080SearchResult.Time = span[0].InnerText;
            forum1080SearchResult.Author = span[1].InnerText;
            forum1080SearchResult.Type = span[2].InnerText;

            return forum1080SearchResult;
        }

        public static async Task<AttmnFileInfo> GetRarInfoFromTxtPath(string txtPath)
        {
            var txtFile = new FileInfo(txtPath);

            var matchTxt = Regex.Match(txtFile.Name, @"(.*[a-z0-9])\.txt", RegexOptions.IgnoreCase);
            if (!matchTxt.Success) return null;

            AttmnFileInfo attmnFileInfo = new(txtPath);

            //读取文件
            using var sr = new StreamReader(txtFile.FullName);
            var originalContent = await sr.ReadToEndAsync();

            ParsingRarDetailContent(attmnFileInfo, originalContent);

            return attmnFileInfo;
        }

        public static async Task<AttmnFileInfo> GetRarInfoFromRarPath(string rarPath)
        {
            AttmnFileInfo attmnFileInfo = new(rarPath);

            await using var stream = File.OpenRead(rarPath);
            using var reader = ReaderFactory.Open(stream, new ReaderOptions()
            {
                LookForHeader = true
            });

            while (reader.MoveToNextEntry())
            {
                var key = reader.Entry.Key;

                if (key.Contains("txt"))
                {
                    //fc2969832.txt
                    var matchTxt = Regex.Match(key, @"(.*[a-z0-9])\.txt", RegexOptions.IgnoreCase);
                    if (!matchTxt.Success) continue;

                    //attmnFileInfo.Name = matchTxt.Groups[1].Value;

                    await using var entryStream = reader.OpenEntryStream();
                    using var sr = new StreamReader(entryStream);
                    var originalContent = await sr.ReadToEndAsync();

                    ParsingRarDetailContent(attmnFileInfo, originalContent);
                }
                else if (key.Contains("png"))
                {
                    //try
                    //{
                    //    string savePath = Path.Combine(AppSettings.X1080XAttmnSavePath, reader.Entry.Key);
                    //    reader.WriteEntryToFile(savePath);
                    //}
                    //catch (Exception ex)
                    //{
                    //    Debug.WriteLine("保存附件图片出错");
                    //}
                }
                //发现字幕，则保存
                else if (reader.Entry.Key.Contains("srt"))
                {
                    try
                    {
                        string savePath = Path.Combine(AppSettings.X1080XAttmnSavePath, reader.Entry.Key);
                        reader.WriteEntryToFile(savePath);
                        attmnFileInfo.SrtPath = savePath;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"保存字幕文件时出错：{ex.Message}");
                    }

                    //long size = reader.Entry.Size;
                    //string shareSha1Link;

                    //using (var destination = new MemoryStream())
                    //{
                    //    //entryStream 不能 seek
                    //    //所以 需要 copyto 新的 Stream
                    //    await using (var entryStream = reader.OpenEntryStream())
                    //    {
                    //        await entryStream.CopyToAsync(destination);
                    //    }

                    //    destination.Seek(0, SeekOrigin.Begin);
                    //    //前一部分Sha1
                    //    int bufferSize = 128 * 1024; //每次读取的字节数
                    //    byte[] buffer = new byte[bufferSize];
                    //    var item = destination.Read(buffer, 0, bufferSize);
                    //    Stream partStream = new MemoryStream(buffer);
                    //    var hashDlg = SHA1.Create();

                    //    byte[] hashBytes = await hashDlg.ComputeHashAsync(partStream);
                    //    var partSha1 = Convert.ToHexString(hashBytes);

                    //    //余下的Sha1
                    //    destination.Seek(0, SeekOrigin.Begin);

                    //    hashBytes = await hashDlg.ComputeHashAsync(destination);

                    //    var allSha1 = Convert.ToHexString(hashBytes);

                    //    shareSha1Link = $"{reader.Entry.Key}|{size}|{allSha1}|{partSha1}";
                    //}

                    //if (attmnFileInfo.Links.TryGetValue("115转存链接", out var link))
                    //{
                    //    link.Add(shareSha1Link);
                    //}
                    //else
                    //{
                    //    attmnFileInfo.Links.Add("115转存链接", new List<string> { shareSha1Link });
                    //}
                }
            }

            return attmnFileInfo;
        }

        private static void ParsingRarDetailContent(AttmnFileInfo attmnFile, string originalContent)
        {
            attmnFile.DetailFileContent = originalContent;

            //获取下载链接
            var contentLine = originalContent.Split('\n');
            for (int i = 0; i < contentLine.Count(); i++)
            {
                var line = contentLine[i].Trim();
                if (line.Contains("解壓密碼："))
                {
                    var passwordResult = Regex.Match(line, @"解壓密碼：(.*)");
                    if (passwordResult.Success)
                    {
                        attmnFile.CompressedPassword = passwordResult.Groups[1].Value;
                    }
                }
                else if (line.Contains("magnet"))
                {
                    string link = line;
                    if (attmnFile.Links.TryGetValue("magnet", out var fileLink))
                    {
                        fileLink.Add(link);
                    }
                    else
                    {
                        attmnFile.Links.Add("magnet", new List<string> { link });
                    }
                }
                else if (line.Contains("ed2k://"))
                {
                    string link = line;
                    if (attmnFile.Links.TryGetValue("ed2k", out var fileLink))
                    {
                        fileLink.Add(link);
                    }
                    else
                    {
                        attmnFile.Links.Add("ed2k", new List<string> { link });
                    }
                }
                else if (line.Contains('|') && Regex.Match(line, @"\w.*\|\d.*?\|\w{40}\|\w{40}").Success)
                {
                    string link = line;
                    if (attmnFile.Links.TryGetValue("115转存链接", out var fileLink))
                    {
                        fileLink.Add(link);
                    }
                    else
                    {
                        attmnFile.Links.Add("115转存链接", new List<string> { link });
                    }
                }
                else if (line.Contains("http"))
                {
                    if (line.Contains("1fichier"))
                    {
                        var linkResult = Regex.Match(line, @"[:： ]+(https?:.*)");
                        if (linkResult.Success)
                        {
                            string link = linkResult.Groups[1].Value;
                            if (attmnFile.Links.TryGetValue("1fichier", out var fileLink))
                            {
                                fileLink.Add(link);
                            }
                            else
                            {
                                attmnFile.Links.Add("1fichier", new List<string> { link });
                            }
                        }
                    }
                    else
                    {
                        var linkResult = Regex.Match(line, @"^http.*");
                        if (linkResult.Success)
                        {
                            string link = linkResult.Value;
                            if (attmnFile.Links.TryGetValue("直链", out var fileLink))
                            {
                                fileLink.Add(link);
                            }
                            else
                            {
                                attmnFile.Links.Add("直链", new List<string> { link });
                            }
                        }
                    }
                }
                else if (line.Contains("http"))
                {
                    var linkResult = Regex.Match(line, @"^http.*");
                    if (linkResult.Success)
                    {
                        string link = linkResult.Groups[1].Value;
                        if (attmnFile.Links.TryGetValue("直链", out var fileLink))
                        {
                            fileLink.Add(link);
                        }   
                        else
                        {
                            attmnFile.Links.Add("直链", new List<string> { link });
                        }
                    }
                }
            }
        }
    }
}
