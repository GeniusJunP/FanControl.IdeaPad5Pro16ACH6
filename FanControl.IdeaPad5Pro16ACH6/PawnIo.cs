using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace FanControl.IdeaPad5Pro16ACH6;

/// <summary>Minimal binding for PawnIOLib.dll, which is installed together with the PawnIO driver.</summary>
internal sealed class PawnIo : IDisposable
{
    private static readonly string LibraryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PawnIO", "PawnIOLib.dll");

    private IntPtr _handle;

    private PawnIo(IntPtr handle) => _handle = handle;

    /// <summary>Opens the driver and loads a signed PawnIO module into it.</summary>
    public static PawnIo Open(byte[] module)
    {
        // resolve PawnIOLib from its install location, so the [DllImport]s below bind to it
        if (LoadLibrary(LibraryPath) == IntPtr.Zero)
            throw new DllNotFoundException($"PawnIO is not installed ({LibraryPath}): {new Win32Exception().Message}");

        Check(NativeMethods.pawnio_open(out IntPtr handle), "open");
        var pawnIo = new PawnIo(handle);
        try
        {
            Check(NativeMethods.pawnio_load(handle, module, (UIntPtr)module.Length), "load");
            return pawnIo;
        }
        catch
        {
            pawnIo.Dispose();
            throw;
        }
    }

    /// <summary>Calls a module function. The module checks the exact input/output sizes.</summary>
    public ulong Execute(string function, int outputCount, params ulong[] input)
    {
        var output = new ulong[Math.Max(1, outputCount)];
        Check(NativeMethods.pawnio_execute(_handle, function, input, (UIntPtr)input.Length, output, (UIntPtr)outputCount, out _), function);
        return output[0];
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero)
            return;
        NativeMethods.pawnio_close(_handle);
        _handle = IntPtr.Zero;
    }

    private static void Check(int hresult, string what)
    {
        if (hresult < 0)
            throw new InvalidOperationException($"PawnIO {what} failed: 0x{hresult:X8}");
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string path);

    private static class NativeMethods
    {
        [DllImport("PawnIOLib")]
        public static extern int pawnio_open(out IntPtr handle);

        [DllImport("PawnIOLib")]
        public static extern int pawnio_load(IntPtr handle, byte[] blob, UIntPtr size);

        [DllImport("PawnIOLib", CharSet = CharSet.Ansi)]
        public static extern int pawnio_execute(IntPtr handle, string name, ulong[] input, UIntPtr inputSize,
            ulong[] output, UIntPtr outputSize, out UIntPtr returnSize);

        [DllImport("PawnIOLib")]
        public static extern int pawnio_close(IntPtr handle);
    }
}
