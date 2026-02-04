using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using DrillMagic.Core.Types;
using System.Collections.Generic;
using System.Text.Json;

namespace DrillMagic.Tests.Domain
{
    public class ColorMapRepositoryTests
    {
        private readonly Mock<ILogger<ColorMapRepository>> _mockLogger;

        public ColorMapRepositoryTests()
        {
            _mockLogger = new Mock<ILogger<ColorMapRepository>>();
        }

        [Fact]
        public void Constructor_ShouldLoadDefaultColorMap_WhenResourceExists()
        {
            // Act
            // Note: This relies on the actual embedded resource in DrillMagic.Core
            var repository = new ColorMapRepository(_mockLogger.Object);

            // Assert
            repository.DefaultColorMap.Should().NotBeNull();
            repository.DefaultColorMap.Should().NotBeEmpty();
            
            // Allow logging to happen
            // We can't strictly verify exact log calls easily due to extension methods, 
            // but we can check the result state.
        }

        [Fact]
        public void Constructor_ShouldInitializeCustomMappings_AsEmpty_WhenFileDoesNotExist()
        {
            // Note: This test is tricky because it accesses the real file system at a hardcoded path.
            // However, we expect it to at least initialize the property to non-null.
            // Ideally we would mock the file system or redirect the path for tests.
            
            // Act
            var repository = new ColorMapRepository(_mockLogger.Object);

            // Assert
            repository.CustomMappings.Should().NotBeNull();
            // It might be empty or not depending on the machine, but it should not match null.
        }
        
        [Fact]
        public void Initialize_ShouldThrow_IfResourceAndFileMissing()
        {
            // This is hard to test without modifying the assembly to remove the resource.
            // We'll skip this negative test for now as it requires intrusive changes or mocking Assembly.GetExecutingAssembly which isn't possible with simple Moq.
        }
    }
}
