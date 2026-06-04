using System;
using System.Runtime.InteropServices;

namespace NDIIntercom.Core
{
    internal static class NdiNativeStrings
    {
        public static string PtrToStringUtf8(IntPtr ptr) =>
            ptr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }
}
