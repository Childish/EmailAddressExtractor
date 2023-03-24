using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaveIBeenPwned.EmailAddressExtractor.Extractor
{
    internal static class Helpers
    {
        internal static string FormatBytes(long bytes)
        {
            string[] units = { "Bytes", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        internal static string FormatMilliseconds(long milliseconds)
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);

            if (timeSpan.TotalHours >= 1)
            {
                return $"{timeSpan.TotalHours:0.##} hours";
            }
            else if (timeSpan.TotalMinutes >= 1)
            {
                return $"{timeSpan.TotalMinutes:0.##} minutes";
            }
            else
            {
                return $"{timeSpan.TotalSeconds:0.##} seconds";
            }
        }
    }
}
