﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Display.Helper.FileProperties.Name;
using Display.Models.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Display.Models.Dto.Settings.Options;

internal class Player : INotifyPropertyChanged
{
    public string Name { get; set; }

    public BitmapIcon Icon { get; set; }

    public PlayerType Own { get; set; }

    private string _path;

    public string Path
    {
        get => _path;
        set
        {
            if (_path == value) return;
            _path = value;
            OnPropertyChanged();
        }
    }

    public Action<string> SavePathAction { get; set; }
    public Func<string> ResetPathFunc { get; set; }

    public bool IsLoadSubOptionOn { get; set; } = true;

    public bool IsNeedPath { get; set; } = true;

    public bool IsChangeQualityEnable { get; set; } = true;

    public async void UploadPathButtonClick(object sender, RoutedEventArgs e)
    {
        var file = await FileMatch.SelectFileAsync(App.AppMainWindow,
            [".exe", ".com"]);

        if (file is null) return;

        Path = file.Path;
        SavePathAction?.Invoke(file.Path);
    }

    public void OpenPathButtonClick(object sender, RoutedEventArgs e)
    {
        FileMatch.LaunchFolder(System.IO.Path.GetDirectoryName(Path));
    }

    public void ResetPathButtonClick(object sender, RoutedEventArgs e)
    {
        Path = ResetPathFunc?.Invoke();
        SavePathAction?.Invoke(Path);
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}
