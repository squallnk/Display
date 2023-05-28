
using Display.Data;
using Display.WindowView;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.ContentsPage.DetailInfo
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FileInfoInCidSmoke : Page
    {
        private string trueName;

        public FileInfoInCidSmoke(string trueName)
        {
            InitializeComponent();

            this.trueName = trueName;

            Loaded += PageLoad;
        }

        private async void PageLoad(object sender, RoutedEventArgs e)
        {
            var videoInfos = await DataAccess.FindFileInfoByTrueName(trueName);

            InfosListView.ItemsSource = videoInfos;
        }

        public static string GetFolderString(string folderCid)
        {
            //从数据库中获取根目录信息
            List<Datum> folderToRootList = DataAccess.GetRootByCid(folderCid);

            return string.Join(" > ", folderToRootList.Select(x=>x.n));
        }

        private void OpenCurrentFolderItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { DataContext: Datum info }) return;

            CommonWindow.CreateAndShowWindow(new DatumList.FileListPage(info.cid));
        }

        private async void DeleteItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem { DataContext: Datum info }) return;

            //115删除
            var dialog = new ContentDialog()
            {
                XamlRoot = XamlRoot,
                Title = "确认",
                PrimaryButtonText = "删除",
                CloseButtonText = "返回",
                DefaultButton = ContentDialogButton.Close,
                Content = "该操作将删除115网盘中的文件，确认删除？"
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary) return;

            // 从115中删除 
            await WebApi.GlobalWebApi.DeleteFiles(info.cid,
                new List<string>() { info.fid });

            // 从数据库中删除
            DataAccess.DeleteDataInFilesInfoAndFileToInfo(trueName);
        }
    }
}
