﻿using EBOM_Macro.Extensions;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class InputsEnabledConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            values.Any(v => v == DependencyProperty.UnsetValue) ||
            (((double)values[0]).IsInExclusiveRange(0.0, 1.0) && !((bool)values[1])) ||
            ((int)values[2]).IsInExclusiveRange(0, (int)values[3]) ? false : true;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
