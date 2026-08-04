using System;
using System.Collections.Generic;
using Xunit;
using Gitic;

namespace Gitic.Tests
{
    public class TablebuilderRefactoredTests
    {
        [Fact]
        public void TestConsoleTableBuilder_NormalBehaviorPreserved()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Name", 10, "left");
            builder.AddColumn("Age", 5, "right");
            builder.AddColumn("Role");

            builder.AddRow(new List<string> { "Alice", "30", "Engineer" });
            builder.AddRow(new List<string> { "Bob", "25", "Designer" });

            var expected = "Name         Age Role\nAlice         30 Engineer\nBob           25 Designer";
            Assert.Equal(expected, builder.Render());
        }

        [Fact]
        public void TestConsoleTableBuilder_WithAnsiCodes_LeftAligned()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Status", 10, "left");
            
            // "\x1B[31mError\x1B[0m" has 5 visible characters ("Error").
            // With width = 10, it should be padded with 5 spaces at the end.
            builder.AddRow(new List<string> { "\x1B[31mError\x1B[0m" });

            var result = builder.Render();
            var lines = result.Split('\n');
            
            Assert.Equal(2, lines.Length);
            // Header is "Status" padded left to width 10 -> "Status    "
            Assert.Equal("Status    ", lines[0]);
            // Row is "\x1B[31mError\x1B[0m     " (5 padding spaces at the end)
            Assert.Equal("\x1B[31mError\x1B[0m     ", lines[1]);
        }

        [Fact]
        public void TestConsoleTableBuilder_WithAnsiCodes_RightAligned()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Count", 8, "right");

            // "\x1B[32m42\x1B[0m" has 2 visible characters ("42").
            // With width = 8, it should be padded with 6 spaces in the front.
            builder.AddRow(new List<string> { "\x1B[32m42\x1B[0m" });

            var result = builder.Render();
            var lines = result.Split('\n');

            Assert.Equal(2, lines.Length);
            // Header is "   Count" (3 padding spaces in the front to reach 8 chars)
            Assert.Equal("   Count", lines[0]);
            // Row is "      \x1B[32m42\x1B[0m" (6 padding spaces in the front)
            Assert.Equal("      \x1B[32m42\x1B[0m", lines[1]);
        }

        [Fact]
        public void TestConsoleTableBuilder_MultipleAnsiCodesInSingleCell()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Mix", 12, "left");

            // "\x1B[1;31mBoldRed\x1B[0m\x1B[32mGreen\x1B[0m"
            // Visible chars: "BoldRedGreen" (length 12)
            // Width is 12, so no padding should be added.
            var cell = "\x1B[1;31mBoldRed\x1B[0m\x1B[32mGreen\x1B[0m";
            builder.AddRow(new List<string> { cell });

            var result = builder.Render();
            var lines = result.Split('\n');

            Assert.Equal("Mix         ", lines[0]); // Header padded to 12
            Assert.Equal(cell, lines[1]); // Cell has visible length 12, so no spaces added
        }

        [Fact]
        public void TestConsoleTableBuilder_NullOrEmptyHandling()
        {
            var builder = new ConsoleTableBuilder();
            builder.AddColumn("Col", 5, "left");
            builder.AddRow(new List<string?> { null });
            builder.AddRow(new List<string?> { "" });

            var result = builder.Render();
            var lines = result.Split('\n');

            Assert.Equal("Col  ", lines[0]); // Width 5
            Assert.Equal("     ", lines[1]); // Null treated as empty and padded with 5 spaces
            Assert.Equal("     ", lines[2]); // Empty padded with 5 spaces
        }

        [Fact]
        public void TestConsoleTableBuilder_InterfaceCovariance()
        {
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

            var result = builder.Render();
            var lines = result.Split('\n');

            Assert.Equal("Col  ", lines[0]);
            Assert.Equal("one  ", lines[1]);
            Assert.Equal("two  ", lines[2]);
            Assert.Equal("three", lines[3]);
            Assert.Equal("four ", lines[4]);
            Assert.Equal("five ", lines[5]);
        }
    }
}
