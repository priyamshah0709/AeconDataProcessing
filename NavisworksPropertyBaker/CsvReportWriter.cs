using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NavisworksPropertyBaker
{
    /// <summary>Tiny CSV writer for report files (unmatched.csv, duplicates.csv, dumps).</summary>
    public static class CsvReportWriter
    {
        public static string Line(params string[] fields)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Quote(fields[i]));
            }
            return sb.ToString();
        }

        public static string Quote(string value)
        {
            if (value == null) return "\"\"";
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static void Write(string path, string headerLine, IEnumerable<string> lines)
        {
            using (var w = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                w.WriteLine(headerLine);
                foreach (string line in lines)
                    w.WriteLine(line);
            }
        }
    }
}
