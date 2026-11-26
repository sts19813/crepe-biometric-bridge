namespace CrepeBiometricBridge;

internal sealed record BridgeSettings(string CrepeUrl, string BridgeKey)
{
    public static BridgeSettings Default { get; } = new("http://127.0.0.1:8000", string.Empty);
}

internal static class BridgeSettingsStore
{
    private const string Filename = "settings.dat";

    public static BridgeSettings Read()
    {
        return EncryptedStore.Read(Filename, BridgeSettings.Default);
    }

    public static void Write(BridgeSettings settings)
    {
        EncryptedStore.Write(Filename, settings);
    }
}
