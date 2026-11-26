using System.Security.Cryptography;
using System.Text.Json;

namespace CrepeBiometricBridge;

internal static class EncryptedStore
{
    private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrepeBiometricBridge");

    public static T Read<T>(string filename, T fallback)
    {
        var pathname = Path.Combine(DirectoryPath, filename);

        if (!File.Exists(pathname))
        {
            return fallback;
        }

        var protectedContents = File.ReadAllBytes(pathname);
        var contents = ProtectedData.Unprotect(protectedContents, null, DataProtectionScope.CurrentUser);

        return JsonSerializer.Deserialize<T>(contents) ?? fallback;
    }

    public static void Write<T>(string filename, T value)
    {
        Directory.CreateDirectory(DirectoryPath);

        var contents = JsonSerializer.SerializeToUtf8Bytes(value);
        var protectedContents = ProtectedData.Protect(contents, null, DataProtectionScope.CurrentUser);

        File.WriteAllBytes(Path.Combine(DirectoryPath, filename), protectedContents);
    }
}
