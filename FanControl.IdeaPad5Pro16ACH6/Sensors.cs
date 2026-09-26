using FanControl.Plugins;

namespace FanControl.IdeaPad5Pro16ACH6;

internal sealed class FanSensor : IPluginSensor
{
    public FanSensor(int fan)
    {
        Id = $"IdeaPad5Pro16ACH6/Fan/{fan + 1}";
        Name = $"Fan {fan + 1}";
    }

    public string Id { get; }

    public string Name { get; }

    public float? Value { get; set; }

    public void Update()
    {
        // read in IdeaPad5ProPlugin.Update, one EC session for all sensors
    }
}

internal sealed class FanControlSensor : IPluginControlSensor2
{
    private readonly IdeaPad5ProPlugin _plugin;
    private readonly int _fan;

    public FanControlSensor(IdeaPad5ProPlugin plugin, int fan, string pairedFanSensorId)
    {
        _plugin = plugin;
        _fan = fan;
        Id = $"IdeaPad5Pro16ACH6/Control/{fan + 1}";
        Name = $"Fan {fan + 1} Control";
        PairedFanSensorId = pairedFanSensorId;
    }

    public string Id { get; }

    public string Name { get; }

    public string PairedFanSensorId { get; }

    public float? Value { get; private set; }

    public void Set(float val)
    {
        Value = val;
        _plugin.Request(_fan, val);
    }

    public void Reset()
    {
        Value = null;
        _plugin.Release(_fan);
    }

    public void Update()
    {
    }
}
