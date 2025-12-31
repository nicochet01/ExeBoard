using System;
using System.Runtime.InteropServices;

public static class ComHelpers
{
    public static void ThrowForHRChecked(int hr, nint errorInfo)
    {
        if (hr >= 0) return;
        Exception? ex = Marshal.GetExceptionForHR(hr, errorInfo);
        if (ex is Exception e)
        {
            throw e;
        }
        // Fallback seguro caso o marshaller tenha retornado algo que não é System.Exception
        throw new COMException($"HRESULT 0x{hr:X8}", hr);
    }
}