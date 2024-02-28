﻿using System.IO;
using Display.Setting.Interfaces;
using DefaultValue = Display.Models.Data.Constant.DefaultSettings;

namespace Display.Setting.Impl;

internal class SavePathSetting : SettingBase, ISavePathSetting
{
    public SavePathSetting(ISettingProvider provider) : base(provider)
    {
    }

    public string ImageSavePath
    {
        get => GetValue(DefaultValue.App.SavePath.Image);
        set => SetValue(value);
    }

    public string SubSavePath
    {
        get => GetValue(DefaultValue.App.SavePath.Sub);
        set => SetValue(value);
    }

    public string AttachmentSavePath
    {
        get => GetValue(DefaultValue.App.SavePath.Attmn);
        set => SetValue(value);
    }

    public string ActorImageSavePath
    {
        get => GetValue(DefaultValue.App.SavePath.Actor);
        set => SetValue(value);
    }

    public string DataSavePath
    {
        get => GetValue(DefaultValue.App.SavePath.Data);
        set => SetValue(value);
    }

    public string ActorFileTreeSavePath
    {
        get => Path.Combine(DataSavePath, "FileTree.json");
        set => SetValue(value);
    }

    public string DataAccessSavePath
    {
        get => GetValue(DefaultValue.App.SavePath.DataAccess);
        set => SetValue(value);
    }
}