﻿using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class LDIFolderPathBrowseEnabledConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var isPathSystemRootRelative = (bool)values[0];

            return !isPathSystemRootRelative || (isPathSystemRootRelative && !string.IsNullOrEmpty((string)values[1]));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
