﻿using System.Collections.Generic;

namespace Display.Models.Dto.Media;

public class UrlOptions
{
    public string Url { get; init; }

    public Dictionary<string, string> Headers { get; init; }

    public bool IsM3U8 => Url.Contains("m3u8");
}