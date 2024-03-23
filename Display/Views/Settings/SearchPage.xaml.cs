using Display.Models.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using Display.Providers.Searcher;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.Views.Settings;


public sealed partial class SearchPage : RootPage
{
    public SearchPage()
    {
        this.InitializeComponent();
    }

    private async void Selected115SavePathButtonClick(object sender, RoutedEventArgs e)
    {
        var contentPage = new SelectedFolderPage();

        var result = await contentPage.ShowContentDialogResult(XamlRoot);

        if (result != ContentDialogResult.Primary) return;

        var explorerItem = contentPage.GetCurrentFolder();
        Debug.WriteLine($"��ǰѡ�У�{explorerItem.Name}({explorerItem.Id})");

        AppSettings.SavePath115Name = explorerItem.Name;

        AppSettings.SavePath115Cid = explorerItem.Id;

        SavePath115CidTextBlock.Description = explorerItem.Name;
        SavePath115CidTextBlock.Text = explorerItem.Id.ToString();

        ShowTeachingTip("���óɹ�");
    }


    private void X1080XBaseUrlChange(object sender, RoutedEventArgs e)
    {
        AppSettings.X1080XBaseUrl = X1080UrlTextBox.Text;

        ShowTeachingTip("�޸����");
    }

    private void X1080XCookieChange(object sender, RoutedEventArgs e)
    {
        AppSettings.X1080XCookie = X1080XCookieTextBox.Text;

        GetInfoFromNetwork.IsX1080XCookieVisible = true;

        X1080X.TryChangedClientHeader("cookie", AppSettings.X1080XCookie);

        ShowTeachingTip("�޸����");
    }

    private void X1080XUserAgentChange(object sender, RoutedEventArgs e)
    {
        AppSettings.X1080XUa = X1080XuaTextBox.Text;

        X1080X.TryChangedClientHeader("user-agent", AppSettings.X1080XCookie);

        ShowTeachingTip("�޸����");
    }

    private async void Save115SavePathButtonClick(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(SavePath115CidTextBlock.Text, out var cid)) return;

        var cidInfo = await WebApi.GlobalWebApi.GetFolderCategory(cid);

        if (cidInfo != null)
        {
            SavePath115CidTextBlock.Description = cidInfo.file_name;
            AppSettings.SavePath115Name = cidInfo.file_name;
            AppSettings.SavePath115Cid = cid;

            ShowTeachingTip("����ɹ�");
        }
        else
        {
            ShowTeachingTip("����ʧ��");
        }
    }

}