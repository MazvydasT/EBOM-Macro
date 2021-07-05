﻿using EBOM_Macro.Extensions;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace EBOM_Macro.Converters
{
    public class DSListExportEnabledConverter : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider) => this;

        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            (((bool?)values[0]) ?? true) == false ||
            (((double)values[1]).IsInExclusiveRange(0.0, 1.0) && !((bool)values[2])) ||
            (((double)values[3]).IsInExclusiveRange(0.0, 1.0) && !((bool)values[4])) ||
            ((double)values[5]) < 1.0 ||
            (((double)values[6]).IsInExclusiveRange(0.0, 1.0) && !((bool)values[7])) ? false : true;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}