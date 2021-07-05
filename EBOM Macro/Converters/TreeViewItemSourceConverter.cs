﻿using EBOM_Macro.Extensions;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class TreeViewItemSourceConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.Yield();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}