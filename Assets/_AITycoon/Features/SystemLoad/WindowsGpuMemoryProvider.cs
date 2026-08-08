#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Windows: Liest die VRAM-Auslastung ALLER aktiven Grafikkarten über die
    /// PDH-Performance-Counter "\GPU Adapter Memory(*)\Dedicated Usage" (Windows 10+).
    /// Die Gesamtkapazität liefert Unity für die primäre GPU (SystemInfo.graphicsMemorySize);
    /// bei Multi-GPU wächst die Kapazität auf die höchste beobachtete Auslastung mit,
    /// damit die Leiste nie über 100 % läuft.
    /// </summary>
    public class WindowsGpuMemoryProvider : ISystemLoadProvider
    {
        public string SourceLabel => "VRAM";

        private const string Pdh = "pdh.dll";
        private const uint PDH_FMT_LARGE = 0x00000400;
        private const uint PDH_MORE_DATA = 0x800007D2;
        private const uint PDH_CSTATUS_VALID_DATA = 0x00000000;
        private const uint PDH_CSTATUS_NEW_DATA = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct PdhFmtCounterValue
        {
            public uint CStatus;
            private uint padding; // Union beginnt bei Offset 8 (64-Bit-Alignment)
            public long largeValue;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PdhFmtCounterValueItem
        {
            public IntPtr szName;
            public PdhFmtCounterValue FmtValue;
        }

        [DllImport(Pdh, CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQueryW(string szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport(Pdh, CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport(Pdh)]
        private static extern uint PdhCollectQueryData(IntPtr hQuery);

        [DllImport(Pdh, CharSet = CharSet.Unicode)]
        private static extern uint PdhGetFormattedCounterArrayW(IntPtr hCounter, uint dwFormat, ref uint lpdwBufferSize, out uint lpdwItemCount, IntPtr itemBuffer);

        [DllImport(Pdh)]
        private static extern uint PdhCloseQuery(IntPtr hQuery);

        private IntPtr query;
        private IntPtr counter;
        private bool initialized;
        private ulong capacityBytes;

        public WindowsGpuMemoryProvider()
        {
            // Kapazität der primären GPU als Startwert (MB -> Bytes).
            int primaryMb = SystemInfo.graphicsMemorySize;
            capacityBytes = primaryMb > 0 ? (ulong)primaryMb * 1024UL * 1024UL : 0;

            if (PdhOpenQueryW(null, IntPtr.Zero, out query) != 0)
                return;

            if (PdhAddEnglishCounterW(query, @"\GPU Adapter Memory(*)\Dedicated Usage", IntPtr.Zero, out counter) != 0)
            {
                PdhCloseQuery(query);
                query = IntPtr.Zero;
                return;
            }

            initialized = PdhCollectQueryData(query) == 0;
        }

        public bool TryGetSample(out SystemLoadSample sample)
        {
            sample = default;
            if (!initialized)
                return false;

            if (PdhCollectQueryData(query) != 0)
                return false;

            // Puffergröße erfragen (erwartet PDH_MORE_DATA), dann Array abholen.
            uint bufferSize = 0;
            uint status = PdhGetFormattedCounterArrayW(counter, PDH_FMT_LARGE, ref bufferSize, out uint itemCount, IntPtr.Zero);
            if (status != PDH_MORE_DATA || bufferSize == 0)
                return false;

            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                status = PdhGetFormattedCounterArrayW(counter, PDH_FMT_LARGE, ref bufferSize, out itemCount, buffer);
                if (status != 0)
                    return false;

                ulong usedBytes = 0;
                int itemSize = Marshal.SizeOf<PdhFmtCounterValueItem>();
                for (int i = 0; i < itemCount; i++)
                {
                    var item = Marshal.PtrToStructure<PdhFmtCounterValueItem>(IntPtr.Add(buffer, i * itemSize));
                    bool valid = item.FmtValue.CStatus == PDH_CSTATUS_VALID_DATA
                              || item.FmtValue.CStatus == PDH_CSTATUS_NEW_DATA;
                    if (valid && item.FmtValue.largeValue > 0)
                        usedBytes += (ulong)item.FmtValue.largeValue;
                }

                // Multi-GPU-Heuristik: Kapazität mindestens auf beobachtete Nutzung anheben.
                if (usedBytes > capacityBytes)
                    capacityBytes = usedBytes;
                if (capacityBytes == 0)
                    return false;

                sample = new SystemLoadSample
                {
                    UsedBytes = usedBytes,
                    TotalBytes = capacityBytes,
                    SourceLabel = SourceLabel
                };
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Dispose()
        {
            if (query != IntPtr.Zero)
            {
                PdhCloseQuery(query);
                query = IntPtr.Zero;
            }
            initialized = false;
        }
    }
}
#endif
