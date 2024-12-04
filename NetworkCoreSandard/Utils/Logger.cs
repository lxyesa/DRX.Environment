using System;
using System.Text;

namespace NetworkCoreStandard.Utils
{
    public class Logger
    {
        private static readonly object _lock = new();

        public static void Log(string header, string message)
        {
            var sb = new StringBuilder(128);
            sb.Append('[')
              .Append(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"))
              .Append(']');

            if (!string.IsNullOrEmpty(header))
            {
                sb.Append(" [")
                  .Append(header)
                  .Append(']');
            }

            sb.Append(' ')
              .Append(message);

            lock (_lock)
            {
                Console.WriteLine(sb.ToString());
            }
        }
    }
}