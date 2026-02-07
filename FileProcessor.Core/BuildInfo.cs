using System.Reflection;
using System.Security.Cryptography;

namespace FileProcessor.Core;

public static class BuildInfo
{
    private static readonly Lazy<string> VersionValue = new(() =>
    {
        var assembly = typeof(BuildInfo).Assembly;
        var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(info) ? assembly.GetName().Version?.ToString() ?? "unknown" : info;
    });

    private static readonly Lazy<string> AssemblyHashValue = new(() =>
    {
        var assembly = typeof(BuildInfo).Assembly;
        var location = assembly.Location;
        if (string.IsNullOrWhiteSpace(location) || !File.Exists(location))
        {
            return "unknown";
        }

        try
        {
            using var stream = File.OpenRead(location);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return "unknown";
        }
    });

    public static string Version => VersionValue.Value;
    public static string AssemblyHash => AssemblyHashValue.Value;
}
