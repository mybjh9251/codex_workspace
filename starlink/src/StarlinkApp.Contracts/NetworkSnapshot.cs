namespace StarlinkApp.Contracts;

public sealed record NetworkSnapshot(
    int ConnectedDeviceCount,
    string Summary,
    IReadOnlyList<NetworkDevice> Devices);
