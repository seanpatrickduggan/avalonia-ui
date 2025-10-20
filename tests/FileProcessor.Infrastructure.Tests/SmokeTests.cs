using FluentAssertions;
using Xunit;

namespace FileProcessor.Infrastructure.Tests;

public class SmokeTests
{
    [Fact]
    public void Project_Loads()
    {
        typeof(FileProcessor.Infrastructure.Workspace.SqliteWorkspaceDb).Should().NotBeNull();
    }
}
