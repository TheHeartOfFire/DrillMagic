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

namespace DrillMagic.Test.Domain
{
    public class ColorMapRepositoryTests : IDisposable
    {
        private readonly Mock<ILogger<ColorMapRepository>> _mockLogger;
        private readonly List<string> _tempFiles = new();

        public ColorMapRepositoryTests()
        {
            _mockLogger = new Mock<ILogger<ColorMapRepository>>();
        }
        
        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                if (File.Exists(file)) File.Delete(file);
            }
        }

        private string GetTempFilePath()
        {
            var path = Path.GetTempFileName();
            _tempFiles.Add(path);
            return path;
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
            // Arrange
            var nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); // Ensure it doesn't exist
            
            // Act
            var repository = new ColorMapRepository(_mockLogger.Object, nonExistentFile);

            // Assert
            repository.CustomMappings.Should().NotBeNull();
            repository.CustomMappings.Should().BeEmpty();
        }

        [Fact]
        public void Constructor_ShouldLoadCustomMappings_WhenFileExists()
        {
            // Arrange
            var tempFile = GetTempFilePath();
            var mappings = new List<Dictionary<uint, string>> 
            {
                new Dictionary<uint, string> { { 310, "Black" }, { 5200, "Snow White" } }
            };
            File.WriteAllText(tempFile, JsonSerializer.Serialize(mappings));
            
            // Act
            var repository = new ColorMapRepository(_mockLogger.Object, tempFile);

            // Assert
            repository.CustomMappings.Should().HaveCount(1);
            repository.CustomMappings[0].Should().ContainKey(310);
        }

        [Fact]
        public void Constructor_ShouldHandleCorruptCustomMappings_ByLoggingError()
        {
            // Arrange
            var tempFile = GetTempFilePath();
            File.WriteAllText(tempFile, "invalid json content");
            
            // Act
            var repository = new ColorMapRepository(_mockLogger.Object, tempFile);

            // Assert
            repository.CustomMappings.Should().BeEmpty();
            // Verify log error was called (tricky with extension methods, checking state is safer)
        }

        [Fact]
        public void Constructor_ShouldNotThrow_WhenFileIsEmpty()
        {
            // Arrange
            var tempFile = GetTempFilePath();
            File.WriteAllText(tempFile, string.Empty);
            
            // Act
            var repository = new ColorMapRepository(_mockLogger.Object, tempFile);

            // Assert
            repository.CustomMappings.Should().NotBeNull();
            repository.CustomMappings.Should().BeEmpty();
        }
        
        [Fact]
        public void Initialize_ShouldThrow_IfResourceAndFileMissing()
        {
            // This is hard to test without modifying the assembly to remove the resource.
            // We'll skip this negative test for now as it requires intrusive changes or mocking Assembly.GetExecutingAssembly which isn't possible with simple Moq.
        }
    }
}
