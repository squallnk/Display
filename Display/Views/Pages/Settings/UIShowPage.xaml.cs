using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Display.Models.Dto.Settings;
using Display.Models.Enums;
using Display.Providers;
using Display.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Display.Views.Pages.Settings;

public sealed partial class UIShowPage
{
    // 首次启动展示的页面
    private readonly List<PageEnumAndName> _startPageList = [
        new PageEnumAndName(NavigationViewItemEnum.HomePage, "主页"),
        new PageEnumAndName(NavigationViewItemEnum.VideoViewPage, "展示"),
        new PageEnumAndName(NavigationViewItemEnum.ActorPage, "演员"),
        new PageEnumAndName(NavigationViewItemEnum.MorePage, "其他"),
        new PageEnumAndName(NavigationViewItemEnum.SettingPage, "设置")
    ];

    private PageEnumAndName DefaultStartPageItem { get; }

    private readonly UIShowSettingViewModel _viewModel = App.GetService<UIShowSettingViewModel>();

    public UIShowPage()
    {
        DefaultStartPageItem = _startPageList.FirstOrDefault(item => item.PageEnum == AppSettings.StartPageEnum);

        InitializeComponent();
    }


    private void UIElement_OnDrop(object sender, DragEventArgs e)
    {
        if (e.OriginalSource is not ListView target) return;

        if (e.DataView.Properties.Values.FirstOrDefault() is not MenuItem moveItem) return;

        var pos = e.GetPosition(target.ItemsPanelRoot);

        var index = 0;
        if (target.Items.Count != 0)
        {
            var sampleItem = (ListViewItem)target.ContainerFromIndex(0);
            var itemHeight = sampleItem.ActualHeight + sampleItem.Margin.Top + sampleItem.Margin.Bottom;
            index = Math.Min(target.Items.Count - 1, (int)(pos.Y / itemHeight));
            var targetItem = (ListViewItem)target.ContainerFromIndex(index);
            var positionInItem = e.GetPosition(targetItem);
            if (positionInItem.Y > itemHeight / 2) index++;
        }

        index = Math.Min(target.Items.Count, index);

        ObservableCollection<MenuItem> srcList;
        ObservableCollection<MenuItem> dstList;

        if (target == MenuItemListView)
        {
            srcList = _viewModel.NavigationItemViewModel.FootMenuItems;
            dstList = _viewModel.NavigationItemViewModel.MenuItems;
        }
        else if (target == FootMenuItemListView)
        {
            srcList = _viewModel.NavigationItemViewModel.MenuItems;
            dstList = _viewModel.NavigationItemViewModel.FootMenuItems;
        }
        else
        {
            return;
        }

        var containItemIndex = target.Items.IndexOf(moveItem);
        if (containItemIndex != -1)
        {
            dstList.Move(containItemIndex, index);
        }
        else
        {
            srcList.Remove(moveItem);
            dstList.Insert(index, moveItem);
        }
    }


    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems is { Count: 0 }) return;

        if (e.AddedItems[0] is PageEnumAndName pageEnumAndName)
        {
            AppSettings.StartPageEnum = pageEnumAndName.PageEnum;
        }
    }


    private void FootListView_OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count != 1 || e.Items[0] is not MenuItem tmp) return;

        e.Data.Properties.Add(nameof(MenuItem), tmp);

        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void MenuItemListView_OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data == null || e.Data.Properties.Count == 0 || e.Data.Properties.First().Value is not MenuItem)
        {
            e.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        e.AcceptedOperation = DataPackageOperation.Move;
        e.DragUIOverride.IsCaptionVisible = false;
        e.DragUIOverride.IsGlyphVisible = false;
    }

    private void MoreMenuItemIsVisibleChanged_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.MoreNavigationItemViewModel.SaveSetting();
    }

    private void MoreMenuItems_OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        _viewModel.MoreNavigationItemViewModel.SaveSetting();
    }

    private void MenuItemIsVisibleChanged_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.NavigationItemViewModel.SaveMenuItemEnumArray();
        _viewModel.NavigationItemViewModel.SaveFootMenuItemEnumArray();
    }

    private void MenuItemListView_OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        _viewModel.NavigationItemViewModel.SaveMenuItemEnumArray();
        _viewModel.NavigationItemViewModel.SaveFootMenuItemEnumArray(); 
    }
}