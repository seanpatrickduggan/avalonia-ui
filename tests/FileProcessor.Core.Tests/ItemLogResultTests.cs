using FileProcessor.Core.Logging;
using FluentAssertions;
using Xunit;

namespace FileProcessor.Core.Tests;

public class ItemLogResultTests
{
    [Fact]
    public void Empty_CreatesResultWithDefaultValues()
    {
        var itemId = "test-item-123";
        var result = ItemLogResult.Empty(itemId);

        result.ItemId.Should().Be(itemId);
        result.HighestSeverity.Should().Be(LogSeverity.Info);
        result.Count.Should().Be(0);
        result.Entries.Should().BeEmpty();
        result.LevelCounts.Should().BeEmpty();
        result.Truncated.Should().BeFalse();
        result.SpillFilePath.Should().BeNull();
    }
}
