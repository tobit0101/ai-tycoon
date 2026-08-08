#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
using System;
using System.Runtime.InteropServices;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// macOS: Liest die systemweite Unified-Memory-Auslastung über die Mach-API
    /// (host_statistics64), analog zur Anzeige im Activity Monitor.
    /// Auf Apple Silicon teilen sich CPU und GPU denselben Speicher — die
    /// RAM-Auslastung IST hier die relevante "VRAM"-Auslastung für lokale LLMs.
    /// </summary>
    public class MacUnifiedMemoryProvider : ISystemLoadProvider
    {
        public string SourceLabel => "Unified Memory";

        private const string Lib = "/usr/lib/libSystem.dylib";
        private const int HOST_VM_INFO64 = 4;

        // Feld-Layout entspricht vm_statistics64 aus xnu/osfmk/mach/vm_statistics.h.
        // Die ersten Zähler sind natural_t (32 Bit), die Event-Zähler 64 Bit.
        [StructLayout(LayoutKind.Sequential)]
        private struct VmStatistics64
        {
            public uint free_count;
            public uint active_count;
            public uint inactive_count;
            public uint wire_count;
            public ulong zero_fill_count;
            public ulong reactivations;
            public ulong pageins;
            public ulong pageouts;
            public ulong faults;
            public ulong cow_faults;
            public ulong lookups;
            public ulong hits;
            public ulong purges;
            public uint purgeable_count;
            public uint speculative_count;
            public ulong decompressions;
            public ulong compressions;
            public ulong swapins;
            public ulong swapouts;
            public uint compressor_page_count;
            public uint throttled_count;
            public uint external_page_count;
            public uint internal_page_count;
            public ulong total_uncompressed_pages_in_compressor;
        }

        [DllImport(Lib)]
        private static extern uint mach_host_self();

        [DllImport(Lib)]
        private static extern int host_statistics64(uint host, int flavor, ref VmStatistics64 info, ref uint count);

        [DllImport(Lib)]
        private static extern int host_page_size(uint host, out IntPtr pageSize);

        [DllImport(Lib, CharSet = CharSet.Ansi)]
        private static extern int sysctlbyname(string name, ref ulong oldp, ref IntPtr oldlenp, IntPtr newp, IntPtr newlen);

        private ulong cachedTotalBytes;

        public bool TryGetSample(out SystemLoadSample sample)
        {
            sample = default;

            uint host = mach_host_self();

            if (host_page_size(host, out IntPtr pageSizePtr) != 0)
                return false;
            ulong pageSize = (ulong)pageSizePtr.ToInt64();
            if (pageSize == 0)
                return false;

            var stats = new VmStatistics64();
            uint count = (uint)(Marshal.SizeOf<VmStatistics64>() / sizeof(int));
            if (host_statistics64(host, HOST_VM_INFO64, ref stats, ref count) != 0)
                return false;

            ulong total = GetTotalMemoryBytes();
            if (total == 0)
                return false;

            // "Belegt" wie im Activity Monitor: Gesamtspeicher minus wirklich freie Seiten
            // und File-Cache (external_page_count). Speculative Pages zählen als frei.
            ulong reclaimablePages = (ulong)(stats.free_count - stats.speculative_count) + stats.external_page_count;
            ulong reclaimableBytes = reclaimablePages * pageSize;
            if (reclaimableBytes > total)
                reclaimableBytes = total;

            sample = new SystemLoadSample
            {
                UsedBytes = total - reclaimableBytes,
                TotalBytes = total,
                SourceLabel = SourceLabel
            };
            return true;
        }

        private ulong GetTotalMemoryBytes()
        {
            if (cachedTotalBytes > 0)
                return cachedTotalBytes;

            ulong value = 0;
            IntPtr size = new IntPtr(sizeof(ulong));
            if (sysctlbyname("hw.memsize", ref value, ref size, IntPtr.Zero, IntPtr.Zero) == 0)
                cachedTotalBytes = value;

            return cachedTotalBytes;
        }

        public void Dispose()
        {
            // Keine nativen Ressourcen zu halten.
        }
    }
}
#endif
