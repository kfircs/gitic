using System;
using System.Collections.Generic;
using Xunit;
using Gitic;
using Kfc.Cli.Terminal;

namespace Gitic.Tests
{
    public class TablebuilderRefactoredTests
    {
        [Fact]
        public void Render_WithNormalInputs_ShouldPreserveNormalBehavior()
        {
            // Arrange
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Name", 10, "left");
            builder.AddColumn("Age", 5, "right");
            builder.AddColumn("Role");

            builder.AddRow(new List<string> { "Alice", "30", "Engineer" });
            builder.AddRow(new List<string> { "Bob", "25", "Designer" });

            var expected = "Name         Age Role\nAlice         30 Engineer\nBob           25 Designer";

            // Act
            var result = builder.Render();

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Render_WithLeftAlignedAnsiCodes_ShouldPadCorrectly()
        {
            // Arrange
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Status", 10, "left");

            // "\x1B[31mError\x1B[0m" has 5 visible characters ("Error").
            // With width = 10, it should be padded with 5 spaces at the end.
            builder.AddRow(new List<string> { "\x1B[31mError\x1B[0m" });

            // Act
            var result = builder.Render();
            var lines = result.Split('\n');

            // Assert
            Assert.Equal(2, lines.Length);
            // Header is "Status" padded left to width 10 -> "Status    "
            Assert.Equal("Status    ", lines[0]);
            // Row is "\x1B[31mError\x1B[0m     " (5 padding spaces at the end)
            Assert.Equal("\x1B[31mError\x1B[0m     ", lines[1]);
        }

        [Fact]
        public void Render_WithRightAlignedAnsiCodes_ShouldPadCorrectly()
        {
            // Arrange
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Count", 8, "right");

            // "\x1B[32m42\x1B[0m" has 2 visible characters ("42").
            // With width = 8, it should be padded with 6 spaces in the front.
            builder.AddRow(new List<string> { "\x1B[32m42\x1B[0m" });

            // Act
            var result = builder.Render();
            var lines = result.Split('\n');

            // Assert
            Assert.Equal(2, lines.Length);
            // Header is "   Count" (3 padding spaces in the front to reach 8 chars)
            Assert.Equal("   Count", lines[0]);
            // Row is "      \x1B[32m42\x1B[0m" (6 padding spaces in the front)
            Assert.Equal("      \x1B[32m42\x1B[0m", lines[1]);
        }

        [Fact]
        public void Render_WithMultipleAnsiCodesInSingleCell_ShouldNotAddExcessPadding()
        {
            // Arrange
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Mix", 12, "left");

            // "\x1B[1;31mBoldRed\x1B[0m\x1B[32mGreen\x1B[0m"
            // Visible chars: "BoldRedGreen" (length 12)
            // Width is 12, so no padding should be added.
            var cell = "\x1B[1;31mBoldRed\x1B[0m\x1B[32mGreen\x1B[0m";
            builder.AddRow(new List<string> { cell });

            // Act
            var result = builder.Render();
            var lines = result.Split('\n');

            // Assert
            Assert.Equal("Mix         ", lines[0]); // Header padded to 12
            Assert.Equal(cell, lines[1]); // Cell has visible length 12, so no spaces added
        }

        [Fact]
        public void Render_WithNullOrEmptyValues_ShouldHandleNullsAsEmpty()
        {
            // Arrange
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Col", 5, "left");
            builder.AddRow(new List<string?> { null });
            builder.AddRow(new List<string?> { "" });

            // Act
            var result = builder.Render();
            var lines = result.Split('\n');

            // Assert
            Assert.Equal("Col  ", lines[0]); // Width 5
            Assert.Equal("     ", lines[1]); // Null treated as empty and padded with 5 spaces
            Assert.Equal("     ", lines[2]); // Empty padded with 5 spaces
        }

        [Fact]
        public void AddRow_WithVariousEnumerableTypes_ShouldCompileAndRenderCorrectly()
        {
            // Arrange
            IConsoleTableBuilder builder = new ConsoleTableBuilder();
            builder.AddColumn("Col", 5, "left");

            // Verify implicit generic covariance of IEnumerable<string?>:
            // 1. List<string> (invariant list of non-nullable)
            builder.AddRow(new List<string> { "one" });

            // 2. string[] (array of non-nullable)
            builder.AddRow(new[] { "two" });

            // 3. IEnumerable<string>
            IEnumerable<string> enumerableNonNullable = new List<string> { "three" };
            builder.AddRow(enumerableNonNullable);

            // 4. List<string?> (invariant list of nullable)
            builder.AddRow(new List<string?> { "four" });

            // 5. IEnumerable<string?>
            IEnumerable<string?> enumerableNullable = new List<string?> { "five" };
            builder.AddRow(enumerableNullable);

            // Act
            var result = builder.Render();
            var lines = result.Split('\n');

            // Assert
            Assert.Equal("Col  ", lines[0]);
            Assert.Equal("one  ", lines[1]);
            Assert.Equal("two  ", lines[2]);
            Assert.Equal("three", lines[3]);
            Assert.Equal("four ", lines[4]);
            Assert.Equal("five ", lines[5]);
        }

        [Fact]
        public void GetVisibleLength_WithAnsiAndPlainStrings_ShouldReturnCorrectLength()
        {
            // Arrange
            var nullStr = (string?)null;
            var emptyStr = "";
            var normalStr = "Hello";
            var ansiStr = "\x1B[31mError\x1B[0m";
            var complexAnsiStr = "\x1B[1;31mBoldRed\x1B[0m\x1B[32mGreen\x1B[0m";

            // Act
            var nullLength = AnsiUtils.GetVisibleLength(nullStr);
            var emptyLength = AnsiUtils.GetVisibleLength(emptyStr);
            var normalLength = AnsiUtils.GetVisibleLength(normalStr);
            var ansiLength = AnsiUtils.GetVisibleLength(ansiStr);
            var complexAnsiLength = AnsiUtils.GetVisibleLength(complexAnsiStr);

            // Assert
            Assert.Equal(0, nullLength);
            Assert.Equal(0, emptyLength);
            Assert.Equal(5, normalLength);
            Assert.Equal(5, ansiLength);
            Assert.Equal(12, complexAnsiLength);
        }

        [Fact]
        public void StripAnsi_WithAnsiAndPlainStrings_ShouldRemoveAnsiCodes()
        {
            // Arrange
            var nullStr = (string?)null;
            var emptyStr = "";
            var normalStr = "Hello";
            var ansiStr = "\x1B[31mError\x1B[0m";
            var complexAnsiStr = "\x1B[1;31mBoldRed\x1B[0m\x1B[32mGreen\x1B[0m";

            // Act
            var nullResult = AnsiUtils.StripAnsi(nullStr);
            var emptyResult = AnsiUtils.StripAnsi(emptyStr);
            var normalResult = AnsiUtils.StripAnsi(normalStr);
            var ansiResult = AnsiUtils.StripAnsi(ansiStr);
            var complexAnsiResult = AnsiUtils.StripAnsi(complexAnsiStr);

            // Assert
            Assert.Equal(string.Empty, nullResult);
            Assert.Equal(string.Empty, emptyResult);
            Assert.Equal("Hello", normalResult);
            Assert.Equal("Error", ansiResult);
            Assert.Equal("BoldRedGreen", complexAnsiResult);
        }
    }
}
