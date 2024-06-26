// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System.Windows.Input;
using Display.Models.Enums;
using Display.Models.Vo.OneOneFive;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using static Display.Models.Vo.OneOneFive.DetailFileInfo;


namespace Display.Controls.UserController;

public sealed partial class FileControl
{
    public static readonly DependencyProperty FolderNameProperty =
        DependencyProperty.Register(nameof(FolderName), typeof(string), typeof(FileControl), null);

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand), typeof(FileControl), null);

    public static readonly DependencyProperty TypeProperty =
        DependencyProperty.Register(nameof(Type), typeof(FileType), typeof(FileControl), null);

    private static readonly DependencyProperty IconTextProperty =
        DependencyProperty.Register(nameof(IconText), typeof(string), typeof(FileControl), null);


    public FileControl()
    {
        this.InitializeComponent();
    }

    public ICommand DeleteCommand
    {
        get => (ICommand)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public FileType Type
    {
        get => (FileType)GetValue(TypeProperty);
        set
        {
            SetValue(TypeProperty, value);

            SetValue(IconTextProperty, Type == FileType.Folder ? "\xE8B7" : "\xE160");
        }
    }
    private string IconText
    {
        get => (string)GetValue(IconTextProperty);
        set => SetValue(IconTextProperty, value);
    }

    public string FolderName
    {
        get => (string)GetValue(FolderNameProperty);
        set => SetValue(FolderNameProperty, value);
    }

    private void FolderControl_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType is PointerDeviceType.Mouse or PointerDeviceType.Pen)
        {
            VisualStateManager.GoToState(sender as Control, "HoverButtonShown", true);
        }
    }

    private void FolderControl_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType is PointerDeviceType.Mouse or PointerDeviceType.Pen)
        {
            VisualStateManager.GoToState(sender as Control, "HoverButtonHidden", true);
        }
    }
}