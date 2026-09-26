using System;
using FanControl.Plugins;

namespace FanControl.IdeaPad5Pro16ACH6;

/// <summary>
/// Fans of the Lenovo IdeaPad 5 Pro 16ACH6 family (ITE IT8987 EC), driven through the EC firmware's manual rpm mode.
/// 0 % = stop, 100 % = full speed (~7300 rpm). The manual bit is shared by both fans: while one control is enabled,
/// a released fan follows the firmware's own target; when every control is released, the firmware takes over again.
/// All EC writes go through <see cref="Apply"/>, called from Set/Reset and every Update, so a busy bus is simply
/// retried on the next cycle.
/// </summary>
public sealed class IdeaPad5ProPlugin : IPlugin2
{
    private const double MaxSetpoint = 73; // rpm / 100 at full duty

    private readonly IPluginLogger _logger;
    private readonly FanSensor[] _fans = new FanSensor[It8987Ec.FanCount];
    private readonly FanControlSensor[] _controls = new FanControlSensor[It8987Ec.FanCount];
    private readonly byte?[] _requested = new byte?[It8987Ec.FanCount]; // null = control released
    private readonly byte?[] _written = new byte?[It8987Ec.FanCount];   // last setpoint written to the EC

    private It8987Ec? _ec;
    private bool _manual;

    public IdeaPad5ProPlugin(IPluginLogger logger)
    {
        _logger = logger;
        for (int fan = 0; fan < It8987Ec.FanCount; fan++)
        {
            _fans[fan] = new FanSensor(fan);
            _controls[fan] = new FanControlSensor(this, fan, _fans[fan].Id);
        }
    }

    public string Name => "IdeaPad 5 Pro 16ACH6";

    public void Initialize()
    {
        _ec = It8987Ec.Open();
        Array.Clear(_requested, 0, _requested.Length);
        Array.Clear(_written, 0, _written.Length);
        _manual = _ec.IsManual();
        if (_manual)
        {
            // left over from a session that ended without Close (e.g. FanControl was killed)
            _logger.Log($"{Name}: manual fan mode was still on, handing the fans back to the firmware");
            ReleaseToFirmware(_ec);
        }
    }

    public void Load(IPluginSensorsContainer container)
    {
        container.FanSensors.AddRange(_fans);
        container.ControlSensors.AddRange(_controls);
    }

    public void Update()
    {
        if (_ec is null)
            return;
        try
        {
            for (int fan = 0; fan < It8987Ec.FanCount; fan++)
                _fans[fan].Value = _ec.ReadRpm(fan);
            Apply(_ec);
        }
        catch (TimeoutException e)
        {
            _logger.Log($"{Name}: {e.Message}, retrying next update");
        }
    }

    public void Close()
    {
        if (_ec is null)
            return;
        try
        {
            if (_manual)
                ReleaseToFirmware(_ec);
        }
        finally
        {
            _ec.Dispose();
            _ec = null;
        }
    }

    internal void Request(int fan, float percent)
    {
        if (float.IsNaN(percent))
            return;
        _requested[fan] = (byte)Math.Round(Math.Max(0, Math.Min(100, percent)) * MaxSetpoint / 100);
        TryApply();
    }

    internal void Release(int fan)
    {
        _requested[fan] = null;
        TryApply();
    }

    private void TryApply()
    {
        if (_ec is null)
            return;
        try
        {
            Apply(_ec);
        }
        catch (TimeoutException e)
        {
            _logger.Log($"{Name}: {e.Message}, applying on the next update");
        }
    }

    /// <summary>Brings the EC in line with the requested state.</summary>
    private void Apply(It8987Ec ec)
    {
        if (Array.TrueForAll(_requested, r => r is null))
        {
            if (_manual)
                ReleaseToFirmware(ec);
            return;
        }

        // setpoints first, so that entering manual mode never runs a fan at a stale value
        for (int fan = 0; fan < It8987Ec.FanCount; fan++)
        {
            byte setpoint = _requested[fan] ?? ec.ReadFirmwareTarget(fan);
            if (_written[fan] != setpoint)
            {
                ec.WriteSetpoint(fan, setpoint);
                _written[fan] = setpoint;
            }
        }
        if (!_manual)
        {
            ec.SetManual(true);
            _manual = true;
        }
    }

    private void ReleaseToFirmware(It8987Ec ec)
    {
        ec.SetManual(false);
        _manual = false;
        for (int fan = 0; fan < It8987Ec.FanCount; fan++)
        {
            ec.WriteSetpoint(fan, 0);
            _written[fan] = 0;
        }
    }
}
