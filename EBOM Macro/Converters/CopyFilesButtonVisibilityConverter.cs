﻿using EBOM_Macro.Extensions;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class CopyFilesButtonVisibilityConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            (values.Any(v => v == DependencyProperty.UnsetValue)) ||
            ((int)values[0]).IsInExclusiveRange(0, (int)values[1]) ? Visibility.Collapsed : Visibility.Visible;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
