using System;
using System.Diagnostics;
using System.Globalization;

namespace i18n.Core.PostBuild.Helpers
{
    internal static class DebugHelpers
    {
        [Conditional("DEBUG")]
        public static void WriteLine(string message)
        {
            var str = $"+++> {DateTime.Now.ToString(CultureInfo.InvariantCulture)} -- {message}";
            Debug.WriteLine(str);
        }
        [Conditional("DEBUG")]
        public static void WriteLine(string format, params object[] args)
        {
            var str = $"+++> {DateTime.Now.ToString(CultureInfo.InvariantCulture)} -- {format}";
            Debug.WriteLine(str, args);
        }
    }
}
