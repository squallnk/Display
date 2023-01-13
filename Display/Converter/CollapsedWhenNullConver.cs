﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Display.Converter;

public class CollapsedWhenNullConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if(value == null)
        {
            return Visibility.Collapsed;
        }
        else if(value is string contentStr)
        {
            if (string.IsNullOrEmpty(contentStr))
            {
                return Visibility.Collapsed;
            }
        }
        else if(value is int contentInt)
        {
            if (contentInt == 0)
            {
                return Visibility.Collapsed;
            }
        }
        

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
