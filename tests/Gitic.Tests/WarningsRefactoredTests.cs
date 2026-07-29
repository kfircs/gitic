using System;
using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests
{
    public class WarningsRefactoredTests
    {
        [Fact]
        public void TestWarningCollector_ParseOrWrapWarning_WithValidAreaWarning()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            var existing = new List<string>
            {
                "Path src/main.cs matched multiple configured areas (Area1, Area2); using Area1."
            };

            var diags = collector.CollectDiagnostics(context, existing);
            
            var target = diags.Find(d => d.Code == "GITIC007");
            Assert.NotNull(target);
            Assert.Equal("Warning", target!.Severity);
            Assert.Equal("Path src/main.cs matched multiple configured areas (Area1, Area2)", target.Message);
            Assert.Equal("using Area1.", target.Hint);
        }

        [Fact]
        public void TestWarningCollector_ParseOrWrapWarning_NoUsingPart()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            var existing = new List<string>
            {
                "Path src/main.cs matched multiple configured areas (Area1, Area2)"
            };

            var diags = collector.CollectDiagnostics(context, existing);
            var target = diags.Find(d => d.Code == "GITIC007");
            Assert.NotNull(target);
            Assert.Equal("Warning", target!.Severity);
            Assert.Equal("Path src/main.cs matched multiple configured areas (Area1, Area2)", target.Message);
            Assert.Equal("Adjust area path patterns in .gitic.yml (or legacy .gitizer.yml) to avoid overlapping patterns.", target.Hint);
        }

        [Fact]
        public void TestWarningCollector_ParseOrWrapWarning_NullAndEmptyWarnings()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            
            var existingWarnings = new List<string>();
            existingWarnings.Add(null!);
            existingWarnings.Add("");

            var diags = collector.CollectDiagnostics(context, existingWarnings);
            
            var targetNull = diags.Find(d => d.Code == "GITIC999" && d.Message == string.Empty);
            Assert.NotNull(targetNull);
        }

        [Fact]
        public void TestWarningCollector_ParseOrWrapWarning_MalformedAreaWarningAtEnd()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            
            var existing = new List<string>
            {
                "matched multiple configured areas; using "
            };

            var diags = collector.CollectDiagnostics(context, existing);
            var target = diags.Find(d => d.Code == "GITIC007");
            Assert.NotNull(target);
            Assert.Equal("matched multiple configured areas", target!.Message);
            Assert.Equal("using ", target.Hint);
        }

        [Fact]
        public void TestWarningCollector_ParseOrWrapWarning_SemicolonUsingNoSpace()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            
            var existing = new List<string>
            {
                "matched multiple configured areas;using"
            };

            var diags = collector.CollectDiagnostics(context, existing);
            var target = diags.Find(d => d.Code == "GITIC007");
            Assert.NotNull(target);
            Assert.Equal("matched multiple configured areas;using", target!.Message);
            Assert.Equal("Adjust area path patterns in .gitic.yml (or legacy .gitizer.yml) to avoid overlapping patterns.", target.Hint);
        }

        [Fact]
        public void TestWarningCollector_ParseOrWrapWarning_StandardUnmatchedWarning()
        {
            var collector = new WarningCollector();
            var context = new WarningContext();
            var existing = new List<string>
            {
                "Some other warning message."
            };

            var diags = collector.CollectDiagnostics(context, existing);
            var target = diags.Find(d => d.Code == "GITIC999");
            Assert.NotNull(target);
            Assert.Equal("Some other warning message.", target!.Message);
            Assert.Null(target.Hint);
        }
    }
}
