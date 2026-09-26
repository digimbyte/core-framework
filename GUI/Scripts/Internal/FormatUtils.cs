
using System;
using System.Globalization;

namespace Aura
{
    internal static class FormatUtils
    {
        public const string FloatFormat = "F2";
        public static readonly IFormatProvider Formatter = CultureInfo.InvariantCulture.NumberFormat;
    }
}
