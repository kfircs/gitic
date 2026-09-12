using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using Gitic;

namespace Gitic.Tests
{
    public class JsonSerializationDefaultsTests
    {
        private class TestSerializationObject
        {
            public string? Name { get; set; }
            public string? Value { get; set; }
        }

        [Fact]
        public void TestCompactOptions_PropertiesAreCorrect()
        {
            // Arrange

            // Act
            var options = JsonSerializationDefaults.Compact;

            // Assert
            Assert.NotNull(options);
            Assert.False(options.WriteIndented);
            Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        }

        [Fact]
        public void TestIndentedOptions_PropertiesAreCorrect()
        {
            // Act
            var options = JsonSerializationDefaults.Indented;

            // Assert
            Assert.NotNull(options);
            Assert.True(options.WriteIndented);
            Assert.Equal(JsonIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        }

        [Fact]
        public void TestCompactOptions_SerializesCorrectly()
        {
            // Arrange
            var obj = new TestSerializationObject
            {
                Name = "TestName",
                Value = null
            };

            // Act
            string json = JsonSerializer.Serialize(obj, JsonSerializationDefaults.Compact);

            // Assert
            // Null value should be ignored, and no indentation (no newlines).
            Assert.Contains("\"Name\":\"TestName\"", json);
            Assert.DoesNotContain("Value", json);
            Assert.DoesNotContain("\n", json);
        }

        [Fact]
        public void TestIndentedOptions_SerializesCorrectly()
        {
            // Arrange
            var obj = new TestSerializationObject
            {
                Name = "TestName",
                Value = null
            };

            // Act
            string json = JsonSerializer.Serialize(obj, JsonSerializationDefaults.Indented);

            // Assert
            // Null value should be ignored, but indented with newlines.
            Assert.Contains("\"Name\": \"TestName\"", json);
            Assert.DoesNotContain("Value", json);
            Assert.Contains("\n", json);
        }

        [Fact]
        public void TestFileMetric_SerializesLinesOfCodeCorrectly()
        {
            // Arrange
            var metric = new FileMetric
            {
                Path = "src/main.cs",
                Lines = 123
            };
            var options = JsonSerializationDefaults.Indented;

            // Act
            string json = JsonSerializer.Serialize(metric, options);

            // Assert
            // Verify both "lines" and "lines_of_code" exist and have the correct value
            Assert.Contains("\"lines\": 123", json);
            Assert.Contains("\"lines_of_code\": 123", json);
        }
    }
}
