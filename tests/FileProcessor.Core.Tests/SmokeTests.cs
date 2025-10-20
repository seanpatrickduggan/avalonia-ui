using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_Loads()
    {
        typeof(FileProcessor.Core.SettingsService).Should().NotBeNull();
    }
}
