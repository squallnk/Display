﻿using Display.Helper.Data;
using Display.Models.Data;
using Display.Models.Records;

namespace Display.Views.Pages.Settings.Account;

public sealed partial class CheckCookie
{
    private CookieKeyValue[] CookieKeyValueArray { get; }

    public CheckCookie(string cookies)
    {
        InitializeComponent();

        CookieKeyValueArray = CookieHelper.FormatCookieArray(cookies);
    }
}