// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.IO;
using Display.Models.Api.OneOneFive.Browser;
using Display.Models.Dto.OneOneFive;
using Display.Views.Windows;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;

namespace Display.Views.Pages.Settings.Account
{
    public sealed partial class VerifyAccountPage
    {
        private static string RequestUrl => $"https://captchaapi.115.com/?ac=security_code&type=web&cb=Close911_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}";

        private bool _isSucceeded;

        private readonly Window _currentWindow;

        private VerifyAccountPage()
        {
            InitializeComponent();

            var window = new CommonWindow("验证账号", 360, 560);
            _currentWindow = window;

            Browser.WebView.Source = new Uri(RequestUrl);
            Browser.WebMessageReceived += Browser_WebMessageReceived;

            window.Content = this;
            window.Closed += Window_Closed;
        }

        public static Window CreateVerifyAccountWindow()
        {
            var verifyAccountPage = new VerifyAccountPage();
            return verifyAccountPage._currentWindow;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            VerifyAccountCompleted?.Invoke(this, _isSucceeded);
        }

        public event EventHandler<bool> VerifyAccountCompleted;
        private async void Browser_WebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponseReceivedEventArgs args)
        {
            if (args.Response == null) return;

            if (!args.Request.Uri.Contains("webapi.115.com/user/captcha")) return;

            if (args.Response == null || args.Response.ReasonPhrase != "OK") return;

            var stream = await args.Response.GetContentAsync();

            var content = stream.AsStreamForRead();

            using TextReader tr = new StreamReader(content);
            var re = await tr.ReadToEndAsync();

            var result = JsonConvert.DeserializeObject<VerifyAccountResult>(re);

            // TODO 在添加任务的异常中可用，但在播放m3u8视频的异常中无效
            if (result is not { State: true }) return;

            _isSucceeded = true;

            _currentWindow.Close();
        }


    }
}
