using System.Diagnostics;

namespace BigFilesGenerator.Monitoring
{
    internal class ProcessInfoProvider
    {
        public static long GetMemoryUsage()
        {
            Process proc = Process.GetCurrentProcess();
            long bytes = proc.PrivateMemorySize64;
            return bytes;
        }

        public static float GetMemoryUsageInMb()
        {
            float bytes = GetMemoryUsage();
            for (int i = 0; i < 2; i++)
            {
                bytes /= 1024;
            }
            return bytes;
        }
    }
}
