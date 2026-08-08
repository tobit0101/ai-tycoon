using System;
using UnityEngine;

namespace AITycoon.Features.SystemLoad
{
    /// <summary>
    /// Eine Momentaufnahme der System-Speicherlast.
    /// Quelle je nach Plattform: Unified Memory (macOS), VRAM (Windows) oder RAM (Fallback).
    /// </summary>
    public struct SystemLoadSample
    {
        public ulong UsedBytes;
        public ulong TotalBytes;
        public string SourceLabel; // z.B. "Unified Memory", "VRAM", "RAM"

        public float Fraction01 => TotalBytes == 0
            ? 0f
            : Mathf.Clamp01((float)((double)UsedBytes / TotalBytes));

        public double UsedGb => UsedBytes / (1024.0 * 1024.0 * 1024.0);
        public double TotalGb => TotalBytes / (1024.0 * 1024.0 * 1024.0);
    }

    /// <summary>
    /// Plattform-Provider für die System-Speicherlast.
    /// </summary>
    public interface ISystemLoadProvider : IDisposable
    {
        string SourceLabel { get; }
        bool TryGetSample(out SystemLoadSample sample);
    }
}
