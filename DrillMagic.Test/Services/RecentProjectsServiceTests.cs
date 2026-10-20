using DrillMagic.Windows.Services;
using FluentAssertions;
using System.IO;
using Xunit;

namespace DrillMagic.Test.Services;

public class RecentProjectsServiceTests
{
    private readonly RecentProjectsService _sut;

    public RecentProjectsServiceTests()
    {
        _sut = new RecentProjectsService();
    }

    [Fact]
    public void AddRecentProject_And_GetRecentProjects_ManagesList()
    {
        // Create temp dummy files so File.Exists checks succeed
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();

        try
        {
            _sut.AddRecentProject(tempFile1);
            _sut.AddRecentProject(tempFile2);

            var recents = _sut.GetRecentProjects();
            recents.Should().Contain(tempFile1);
            recents.Should().Contain(tempFile2);
            // Most recent is inserted first
            recents[0].Should().Be(tempFile2);

            _sut.RemoveRecentProject(tempFile1);
            var updated = _sut.GetRecentProjects();
            updated.Should().NotContain(tempFile1);
            updated.Should().Contain(tempFile2);
        }
        finally
        {
            if (File.Exists(tempFile1)) File.Delete(tempFile1);
            if (File.Exists(tempFile2)) File.Delete(tempFile2);
        }
    }
}

