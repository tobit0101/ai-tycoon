#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Windows-Fallback: Systemweite RAM-Auslastung über GlobalMemoryStatusEx,
    /// falls die GPU-Performance-Counter nicht verfügbar sind (z.B. iGPU ohne
    /// dediziertes VRAM oder alte Treiber).
    /// </summary>
    public class WindowsRamProvider : ISystemLoadProvider
    {
        public string SourceLabel => "RAM";

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        public bool TryGetSample(out SystemLoadSample sample)
        {
            sample = default;

            var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
                return false;

            sample = new SystemLoadSample
            {
                UsedBytes = status.ullTotalPhys - status.ullAvailPhys,
                TotalBytes = status.ullTotalPhys,
                SourceLabel = SourceLabel
            };
            return true;
        }

        public void Dispose()
        {
            // Keine nativen Ressourcen zu halten.
        }
    }
}
#endif
