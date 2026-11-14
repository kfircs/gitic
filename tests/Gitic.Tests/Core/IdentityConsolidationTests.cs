using System.Collections.Generic;
using Xunit;

namespace Gitic.Tests;

public class IdentityConsolidationTests
{
    private static GitIdentity Identity(string name, string email) => new() { Name = name, Email = email };

    [Fact]
    public void Resolve_LastCommaFirstAndFirstLastWithSameEmail_ShareOneIdentity()
    {
        var registry = new IdentityRegistry();

        var commaForm = registry.Resolve(Identity("Lahav, Roei", "rlahav@example.com"));
        var plainForm = registry.Resolve(Identity("Roei Lahav", "rlahav@example.com"));

        Assert.Equal("Roei Lahav", commaForm.Name);
        Assert.Equal(IdentityUtils.IdentityKey(commaForm), IdentityUtils.IdentityKey(plainForm));
    }

    [Fact]
    public void Resolve_LastCommaFirstWithDifferentEmail_StaysSeparate()
    {
        var registry = new IdentityRegistry();

        var first = registry.Resolve(Identity("Lahav, Roei", "one@example.com"));
        var second = registry.Resolve(Identity("Roei Lahav", "two@example.com"));

        Assert.NotEqual(IdentityUtils.IdentityKey(first), IdentityUtils.IdentityKey(second));
    }

    [Theory]
    [InlineData("Roei Lahav")]
    [InlineData("kfd")]
    [InlineData("a, b, c")]
    [InlineData(", Roei")]
    [InlineData("Lahav,")]
    public void Resolve_NamesWithoutExactlyOneNonEmptyCommaPair_AreLeftUnchanged(string name)
    {
        var registry = new IdentityRegistry();

        Assert.Equal(name, registry.Resolve(Identity(name, "x@example.com")).Name);
    }

    [Fact]
    public void Resolve_AliasConfiguredWithTheRawCommaName_StillMatches()
    {
        var registry = new IdentityRegistry(aliases: new List<AliasRule>
        {
            new()
            {
                Canonical = Identity("Kfir Chen", "kfchen@example.com"),
                Identities = new List<GitIdentity> { Identity("Chen, Kfir", "kfir@chen") }
            }
        });

        var resolved = registry.Resolve(Identity("Chen, Kfir", "kfir@chen"));

        Assert.Equal("Kfir Chen", resolved.Name);
        Assert.Equal("kfchen@example.com", resolved.Email);
    }

    [Fact]
    public void Resolve_WithMergeOnEmail_MergesDifferentNamesSharingAnEmailIntoTheFirstSeen()
    {
        var registry = new IdentityRegistry(mergeOnEmail: true);

        var first = registry.Resolve(Identity("hpeled_deloitte", "hpeled@example.com"));
        var second = registry.Resolve(Identity("Peled, Hila", "hpeled@example.com"));

        Assert.Equal(IdentityUtils.IdentityKey(first), IdentityUtils.IdentityKey(second));
    }

    [Theory]
    [InlineData("Claude Sonnet 5.5", "noreply@anthropic.com")]
    [InlineData("Claude Sonnet 4.6", "noreply@anthropic.com")]
    public void IsBot_ClaudeCoAuthor_IsTreatedAsAnAgent(string name, string email)
    {
        Assert.True(new IdentityRegistry().IsBot(Identity(name, email)));
    }

    [Fact]
    public void IsBot_PersonNamedClaudeOnAnotherDomain_IsNotAnAgent()
    {
        Assert.False(new IdentityRegistry().IsBot(Identity("Claude Dupont", "claude@example.com")));
    }
}
