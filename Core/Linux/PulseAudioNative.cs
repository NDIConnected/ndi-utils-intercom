using System;
using System.Runtime.InteropServices;

namespace NDIIntercom.Core.Linux
{
    /// <summary>
    /// Minimal PulseAudio Simple API — works with PipeWire via the PulseAudio compatibility layer.
    /// </summary>
    internal static class PulseAudioNative
    {
        internal const string SimpleLibrary = "libpulse-simple.so.0";
        internal const string PulseLibrary = "libpulse.so.0";

        internal const int PA_STREAM_PLAYBACK = 0;
        internal const int PA_STREAM_RECORD = 1;

        /// <summary>PA_SAMPLE_FLOAT32LE (see pulse/sample.h)</summary>
        internal const int PA_SAMPLE_FLOAT32LE = 5;
        /// <summary>PA_SAMPLE_S16LE (see pulse/sample.h)</summary>
        internal const int PA_SAMPLE_S16LE = 3;

        [StructLayout(LayoutKind.Sequential)]
        internal struct pa_sample_spec
        {
            public int format;
            public uint rate;
            public byte channels;
            private readonly byte _pad0;
            private readonly byte _pad1;
            private readonly byte _pad2;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct pa_buffer_attr
        {
            public uint maxlength;
            public uint tlength;
            public uint prebuf;
            public uint minreq;
            public uint fragsize;
        }

        [DllImport(SimpleLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr pa_simple_new(
            IntPtr server,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            int dir,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? dev,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string streamName,
            ref pa_sample_spec ss,
            IntPtr map,
            IntPtr attr,
            out int error);

        /// <summary>Overload accepting a pinned <see cref="pa_buffer_attr"/> pointer.</summary>
        [DllImport(SimpleLibrary, CallingConvention = CallingConvention.Cdecl, EntryPoint = "pa_simple_new")]
        internal static extern IntPtr pa_simple_new_with_attr(
            IntPtr server,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            int dir,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? dev,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string streamName,
            ref pa_sample_spec ss,
            IntPtr map,
            ref pa_buffer_attr attr,
            out int error);

        [DllImport(SimpleLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int pa_simple_read(IntPtr s, [Out] byte[] data, UIntPtr bytes, out int error);

        [DllImport(SimpleLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int pa_simple_write(IntPtr s, byte[] data, UIntPtr bytes, out int error);

        [DllImport(SimpleLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int pa_simple_flush(IntPtr s, out int error);

        [DllImport(SimpleLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void pa_simple_free(IntPtr s);

        [DllImport(PulseLibrary, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr pa_strerror(int error);

        internal static string? StrError(int error)
        {
            IntPtr p = pa_strerror(error);
            return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
        }
    }
}
