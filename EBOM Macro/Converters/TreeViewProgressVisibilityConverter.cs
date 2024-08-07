﻿using EBOM_Macro.Extensions;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class TreeViewProgressVisibilityConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            values.All(v => v != DependencyProperty.UnsetValue) &&
            !string.IsNullOrWhiteSpace((string)values[5]) &&
            ((((double)values[0]).IsInExclusiveRange(0.0, 1.0) && !(bool)values[1]) ||
            (((double)values[2]).IsInExclusiveRange(0.0, 1.0) && !(bool)values[3]) ||
            ((double)values[4]) < 1.0) ? Visibility.Visible : Visibility.Collapsed;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
