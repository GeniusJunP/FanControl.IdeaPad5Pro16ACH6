using System;
using System.IO;
using System.Threading;

namespace FanControl.IdeaPad5Pro16ACH6;

/// <summary>
/// ITE IT8987 EC of the Lenovo IdeaPad 5 Pro 16ACH6 family, accessed through ITE I2EC
/// (index/data ports 0x4E/0x4F) with the signed PawnIO LpcIO module.
/// The EC firmware has a manual rpm mode: with 0x4F2 bit3 set, its fan servo follows
/// the setpoints 0x88E/0x88F (rpm / 100) instead of its own temperature curve.
/// </summary>
internal sealed class It8987Ec : IDisposable
{
    public const int FanCount = 2;

    private const byte IndexPort = 0x4E;
    private const byte DataPort = 0x4F;
    private const int ExpectedChipId = 0x8987;
    private const ushort ChipIdHigh = 0x2000;
    private const ushort ChipIdLow = 0x2001;
    private const ushort ModeFlags = 0x4F2;
    private const byte ManualBit = 0x08;
    private const double TachToRpm = 2_156_000;
    private static readonly ushort[] Setpoint = { 0x88E, 0x88F };
    private static readonly ushort[] FirmwareTarget = { 0x884, 0x885 };
    private static readonly ushort[] Tach = { 0x181E, 0x1820 };

    // same mutex that LibreHardwareMonitor / FanControl take for Super I/O and LPC port access
    private static readonly TimeSpan BusTimeout = TimeSpan.FromMilliseconds(500);
    private readonly Mutex _isaBus = new(false, @"Global\Access_ISABUS.HTP.Method");
    private readonly PawnIo _pawnIo;

    private It8987Ec(PawnIo pawnIo) => _pawnIo = pawnIo;

    public static It8987Ec Open()
    {
        var ec = new It8987Ec(PawnIo.Open(LoadModule()));
        try
        {
            ec._pawnIo.Execute("ioctl_select_slot", 0, 1); // slot 1 = register port 0x4E
            int chipId = ec.Locked(() => ec.ReadByte(ChipIdHigh) << 8 | ec.ReadByte(ChipIdLow));
            if (chipId != ExpectedChipId)
                throw new NotSupportedException($"ITE IT8987 EC not found on port 0x4E (chip id 0x{chipId:X4}).");
            return ec;
        }
        catch
        {
            ec.Dispose();
            throw;
        }
    }

    public int ReadRpm(int fan)
    {
        int tach = Locked(() => ReadByte(Tach[fan]) | ReadByte((ushort)(Tach[fan] + 1)) << 8);
        return tach is 0 or 0xFFFF ? 0 : (int)Math.Round(TachToRpm / tach);
    }

    /// <summary>The target (rpm / 100) the firmware's own curve currently asks for.</summary>
    public byte ReadFirmwareTarget(int fan) => Locked(() => ReadByte(FirmwareTarget[fan]));

    public void WriteSetpoint(int fan, byte rpmDiv100) => Locked(() => WriteByte(Setpoint[fan], rpmDiv100));

    public bool IsManual() => Locked(() => (ReadByte(ModeFlags) & ManualBit) != 0);

    /// <summary>Sets or clears only the manual bit of 0x4F2 (read-modify-write under one lock).</summary>
    public void SetManual(bool on) => Locked(() =>
    {
        byte flags = ReadByte(ModeFlags);
        byte updated = on ? (byte)(flags | ManualBit) : (byte)(flags & ~ManualBit);
        if (updated != flags)
            WriteByte(ModeFlags, updated);
    });

    public void Dispose()
    {
        _pawnIo.Dispose();
        _isaBus.Dispose();
    }

    private void Locked(Action action) => Locked(() => { action(); return 0; });

    private T Locked<T>(Func<T> action)
    {
        if (!_isaBus.WaitOne(BusTimeout))
            throw new TimeoutException("ISA bus mutex is busy");
        try
        {
            return action();
        }
        finally
        {
            _isaBus.ReleaseMutex();
        }
    }

    // ITE I2EC: address high byte -> D2 register 0x11, low byte -> 0x10, data -> 0x12
    private byte ReadByte(ushort address)
    {
        SelectAddress(address);
        return (byte)_pawnIo.Execute("ioctl_pio_inb", 1, DataPort);
    }

    private void WriteByte(ushort address, byte value)
    {
        SelectAddress(address);
        Out(DataPort, value);
    }

    private void SelectAddress(ushort address)
    {
        WriteD2(0x11, (byte)(address >> 8));
        WriteD2(0x10, (byte)address);
        Out(IndexPort, 0x2E);
        Out(DataPort, 0x12);
        Out(IndexPort, 0x2F);
    }

    private void WriteD2(byte register, byte value)
    {
        Out(IndexPort, 0x2E);
        Out(DataPort, register);
        Out(IndexPort, 0x2F);
        Out(DataPort, value);
    }

    private void Out(byte port, byte value) => _pawnIo.Execute("ioctl_pio_outb", 0, port, value);

    private static byte[] LoadModule()
    {
        using var resource = typeof(It8987Ec).Assembly.GetManifestResourceStream("LpcIO.bin")
            ?? throw new InvalidOperationException("embedded LpcIO.bin is missing");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }
}
