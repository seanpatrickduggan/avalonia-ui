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

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }

    private SettingsService CreateService()
    {
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        return new SettingsService(settingsFile);
    }

    [Fact]
    public void Constructor_LoadsSettings_WhenFileExists()
    {
        // Arrange
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        var appSettings = new AppSettings
        {
            ActiveWorkspace = "/some/path",
            Workspaces = new List<WorkspaceInfo> { new WorkspaceInfo { Path = "/some/path", Name = "Test", IsActive = true } },
            CoreSpareCount = 2
        };
        var json = JsonSerializer.Serialize(appSettings);
        File.WriteAllText(settingsFile, json);

        // Act
        var service = new SettingsService(settingsFile);

        // Assert
        service.WorkspaceDirectory.Should().Be("/some/path");
        service.Workspaces.Should().HaveCount(1);
        service.CoreSpareCount.Should().Be(2);
    }

    [Fact]
    public void Constructor_HandlesInvalidJson_Gracefully()
    {
        // Arrange
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsFile, "invalid json");

        // Act
        var service = new SettingsService(settingsFile);

        // Assert
        service.WorkspaceDirectory.Should().BeNull();
        service.Workspaces.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_UsesDefaultSettings_WhenFileNotExists()
    {
        // Arrange
        var settingsFile = Path.Combine(_tempDir, "nonexistent.json");

        // Act
        var service = new SettingsService(settingsFile);

        // Assert
        service.WorkspaceDirectory.Should().BeNull();
        service.Workspaces.Should().BeEmpty();
        service.CoreSpareCount.Should().Be(1);
    }

    [Fact]
    public void WorkspaceDirectory_Getter_ReturnsActiveWorkspace()
    {
        var service = CreateService();
        service.WorkspaceDirectory = "/test/path";
        service.WorkspaceDirectory.Should().Be("/test/path");
    }

    [Fact]
    public void WorkspaceDirectory_Setter_UpdatesActiveWorkspace_And_IsActiveFlags()
    {
        var service = CreateService();
        service.Workspaces.Add(new WorkspaceInfo { Path = "/path1", Name = "WS1" });
        service.Workspaces.Add(new WorkspaceInfo { Path = "/path2", Name = "WS2" });

        service.WorkspaceDirectory = "/path2";

        service.WorkspaceDirectory.Should().Be("/path2");
        service.Workspaces[0].IsActive.Should().BeFalse();
        service.Workspaces[1].IsActive.Should().BeTrue();
    }

    [Fact]
    public void InputDirectory_ReturnsNull_WhenNoWorkspace()
    {
        var service = CreateService();
        service.InputDirectory.Should().BeNull();
    }

    [Fact]
    public void InputDirectory_ReturnsPath_WhenWorkspaceSet()
    {
        var service = CreateService();
        service.WorkspaceDirectory = "/workspace";
        service.InputDirectory.Should().Be(Path.Combine("/workspace", "input"));
    }

    [Fact]
    public void ProcessedDirectory_ReturnsNull_WhenNoWorkspace()
    {
        var service = CreateService();
        service.ProcessedDirectory.Should().BeNull();
    }

    [Fact]
    public void ProcessedDirectory_ReturnsPath_WhenWorkspaceSet()
    {
        var service = CreateService();
        service.WorkspaceDirectory = "/workspace";
        service.ProcessedDirectory.Should().Be(Path.Combine("/workspace", "processed"));
    }

    [Fact]
    public void CoreSpareCount_Setter_ClampsValue()
    {
        var service = CreateService();
        service.CoreSpareCount = -1;
        service.CoreSpareCount.Should().Be(0);

        service.CoreSpareCount = Environment.ProcessorCount + 1;
        service.CoreSpareCount.Should().Be(Environment.ProcessorCount - 1);
    }

    [Fact]
    public void MaxDegreeOfParallelism_CalculatesCorrectly()
    {
        var service = CreateService();
        service.CoreSpareCount = 1;
        service.MaxDegreeOfParallelism.Should().Be(Math.Max(1, Environment.ProcessorCount - 1));
    }

    [Fact]
    public async Task SaveSettingsAsync_WritesToFile()
    {
        var service = CreateService();
        service.WorkspaceDirectory = "/test";
        service.CoreSpareCount = 2;

        await service.SaveSettingsAsync();

        var settingsFile = Path.Combine(_tempDir, "settings.json");
        File.Exists(settingsFile).Should().BeTrue();
        var json = File.ReadAllText(settingsFile);
        var settings = JsonSerializer.Deserialize<AppSettings>(json);
        settings.Should().NotBeNull();
        settings!.ActiveWorkspace.Should().Be("/test");
        settings.CoreSpareCount.Should().Be(2);
    }

    [Fact]
    public async Task SaveSettingsAsync_HandlesException_Gracefully()
    {
        var service = new SettingsService("/invalid/path/settings.json");
        // Should not throw
        await service.SaveSettingsAsync();
    }

    [Fact]
    public async Task LoadSettingsAsync_LoadsFromFile()
    {
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        var appSettings = new AppSettings
        {
            ActiveWorkspace = "/loaded/path",
            Workspaces = new List<WorkspaceInfo> { new WorkspaceInfo { Path = "/loaded/path", Name = "Loaded", IsActive = true } }
        };
        var json = JsonSerializer.Serialize(appSettings);
        File.WriteAllText(settingsFile, json);

        var service = CreateService();
        await service.LoadSettingsAsync();

        service.WorkspaceDirectory.Should().Be("/loaded/path");
        service.Workspaces.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadSettingsAsync_HandlesInvalidJson_Gracefully()
    {
        var settingsFile = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(settingsFile, "invalid");

        var service = CreateService();
        await service.LoadSettingsAsync();

        // Should not crash, keep defaults
        service.WorkspaceDirectory.Should().BeNull();
    }

    [Fact]
    public async Task AddWorkspaceAsync_ReturnsFalse_ForEmptyPath()
    {
        var service = CreateService();
        var result = await service.AddWorkspaceAsync("");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddWorkspaceAsync_ReturnsFalse_ForWhitespacePath()
    {
        var service = CreateService();
        var result = await service.AddWorkspaceAsync("   ");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddWorkspaceAsync_CreatesDirectories_And_SetsActive()
    {
        var service = CreateService();
        var wsPath = Path.Combine(_tempDir, "new_workspace");

        var result = await service.AddWorkspaceAsync(wsPath, "New WS");

        result.Should().BeTrue();
        Directory.Exists(wsPath).Should().BeTrue();
        Directory.Exists(Path.Combine(wsPath, "input")).Should().BeTrue();
        Directory.Exists(Path.Combine(wsPath, "processed")).Should().BeTrue();
        service.WorkspaceDirectory.Should().Be(wsPath);
        service.Workspaces.Should().ContainSingle(w => w.Path == wsPath && w.Name == "New WS" && w.IsActive);
    }

    [Fact]
    public async Task AddWorkspaceAsync_SetsExistingWorkspaceActive_IfAlreadyExists()
    {
        var service = CreateService();
        var wsPath = Path.Combine(_tempDir, "existing");
        Directory.CreateDirectory(wsPath);

        // Add first time
        await service.AddWorkspaceAsync(wsPath);
        service.Workspaces.Should().HaveCount(1);

        // Add again
        var result = await service.AddWorkspaceAsync(wsPath);
        result.Should().BeTrue();
        service.Workspaces.Should().HaveCount(1); // Still one
        service.WorkspaceDirectory.Should().Be(wsPath);
    }

    [Fact]
    public async Task AddWorkspaceAsync_UsesPathName_WhenNameNotProvided()
    {
        var service = CreateService();
        var wsPath = Path.Combine(_tempDir, "workspace_name");

        await service.AddWorkspaceAsync(wsPath);

        service.Workspaces[0].Name.Should().Be("workspace_name");
    }

    [Fact]
    public async Task AddWorkspaceAsync_HandlesException_ReturnsFalse()
    {
        var service = CreateService();
        // Try to add a path that can't be created (e.g., invalid chars)
        var invalidPath = Path.Combine(_tempDir, "invalid\0path");

        var result = await service.AddWorkspaceAsync(invalidPath);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveWorkspaceAsync_ReturnsFalse_ForNonexistentWorkspace()
    {
        var service = CreateService();
        var result = await service.SetActiveWorkspaceAsync("/nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveWorkspaceAsync_ReturnsFalse_WhenDirectoryNotExists()
    {
        var service = CreateService();
        service.Workspaces.Add(new WorkspaceInfo { Path = "/nonexistent", Name = "Test" });

        var result = await service.SetActiveWorkspaceAsync("/nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveWorkspaceAsync_SetsActive_And_Saves()
    {
        var service = CreateService();
        var wsPath = Path.Combine(_tempDir, "ws1");
        Directory.CreateDirectory(wsPath);
        service.Workspaces.Add(new WorkspaceInfo { Path = wsPath, Name = "WS1" });
        service.Workspaces.Add(new WorkspaceInfo { Path = Path.Combine(_tempDir, "ws2"), Name = "WS2" });

        var result = await service.SetActiveWorkspaceAsync(wsPath);

        result.Should().BeTrue();
        service.WorkspaceDirectory.Should().Be(wsPath);
        service.Workspaces[0].IsActive.Should().BeTrue();
        service.Workspaces[1].IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetActiveWorkspaceAsync_Overload_SetsWorkspace()
    {
        var service = CreateService();
        var wsPath = Path.Combine(_tempDir, "ws");
        Directory.CreateDirectory(wsPath);
        var workspace = new WorkspaceInfo { Path = wsPath, Name = "WS" };
        service.Workspaces.Add(workspace);

        await service.SetActiveWorkspaceAsync(workspace);

        service.WorkspaceDirectory.Should().Be(wsPath);
        workspace.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveWorkspaceAsync_ReturnsFalse_ForNonexistent()
    {
        var service = CreateService();
        var result = await service.RemoveWorkspaceAsync("/nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveWorkspaceAsync_RemovesWorkspace_And_UpdatesActive()
    {
        var service = CreateService();
        var ws1 = Path.Combine(_tempDir, "ws1");
        var ws2 = Path.Combine(_tempDir, "ws2");
        Directory.CreateDirectory(ws1);
        Directory.CreateDirectory(ws2);
        service.Workspaces.Add(new WorkspaceInfo { Path = ws1, Name = "WS1" });
        service.Workspaces.Add(new WorkspaceInfo { Path = ws2, Name = "WS2" });
        service.WorkspaceDirectory = ws1;

        var result = await service.RemoveWorkspaceAsync(ws1);

        result.Should().BeTrue();
        service.Workspaces.Should().HaveCount(1);
        service.Workspaces[0].Path.Should().Be(ws2);
        service.WorkspaceDirectory.Should().Be(ws2);
    }

    [Fact]
    public async Task RemoveWorkspaceAsync_Overload_RemovesWorkspace()
    {
        var service = CreateService();
        var workspace = new WorkspaceInfo { Path = "/test", Name = "Test" };
        service.Workspaces.Add(workspace);

        await service.RemoveWorkspaceAsync(workspace);

        service.Workspaces.Should().BeEmpty();
    }

    [Fact]
    public void Instance_ReturnsSingleton()
    {
        var instance1 = SettingsService.Instance;
        var instance2 = SettingsService.Instance;
        instance1.Should().BeSameAs(instance2);
    }
}
