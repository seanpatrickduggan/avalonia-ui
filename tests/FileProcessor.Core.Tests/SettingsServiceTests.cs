using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FileProcessor.Core;
using FileProcessor.Core.Interfaces;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class SettingsServiceTests
{
    private static string TempSettingsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static SettingsService CreateWithTempStorage(string baseDir)
    {
        var settingsFile = Path.Combine(baseDir, "settings.json");
        return new SettingsService(settingsFile);
    }

    [Fact]
    public async Task AddWorkspace_SetsActive_And_Persists()
    {
        var dir = TempSettingsDir();
        try
        {
            var svc = CreateWithTempStorage(dir);
            var wsPath = Path.GetFullPath(Path.Combine(dir, "ws"));
            var ok = await svc.AddWorkspaceAsync(wsPath, name: "TestWS");
            ok.Should().BeTrue();
            svc.WorkspaceDirectory.Should().Be(wsPath);
            svc.Workspaces.Should().ContainSingle(w => w.Path == wsPath && w.IsActive);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
