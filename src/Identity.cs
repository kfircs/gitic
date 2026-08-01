using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Gitic.Tests")]

namespace Gitic
{
    internal interface IIdentityKeyGenerator
    {
        string IdentityKey(GitIdentity identity);
        string IdentityKey(string name, string email);
    }

    internal class DefaultIdentityKeyGenerator : IIdentityKeyGenerator
    {
        public string IdentityKey(GitIdentity identity) => identity is null ? string.Empty : IdentityKey(identity.Name, identity.Email);

        public string IdentityKey(string name, string email) => $"{(name?.ToLowerInvariant() ?? string.Empty)} <{(email?.ToLowerInvariant() ?? string.Empty)}>";
    }

    internal static class IdentityKeyGenerator
    {
        public static IIdentityKeyGenerator Default { get; } = new DefaultIdentityKeyGenerator();
    }

    internal static class IdentityUtils
    {
        public static string IdentityKey(GitIdentity identity)
        {
            return IdentityKeyGenerator.Default.IdentityKey(identity);
        }

        public static string IdentityKey(string name, string email)
        {
            return IdentityKeyGenerator.Default.IdentityKey(name, email);
        }

        public static bool SameIdentity(GitIdentity left, GitIdentity right) => left.Name.Equals(right.Name, StringComparison.OrdinalIgnoreCase) && left.Email.Equals(right.Email, StringComparison.OrdinalIgnoreCase);

        public static GitIdentity ResolveAlias(GitIdentity identity, List<AliasRule> aliases)
        {
            foreach (var alias in aliases)
            {
                if (SameIdentity(identity, alias.Canonical) ||
                    alias.Identities.Any(candidate => SameIdentity(identity, candidate)))
                {
                    return alias.Canonical;
                }
            }
            return identity;
        }

        private const string GithubNoreplySuffix = "@users.noreply.github.com";

        private static readonly string[] DefaultBotKeywords = new[]
        {
            "[bot]",
            " bot",
            "-bot@",
            "bot@",
            "dependabot",
            "renovate",
            "copilot",
            "github-actions",
            "github actions",
            "gitlab-ci",
            "gitlab ci",
            "jenkins",
            "circleci"
        };

        private static bool MatchesDefaultBotHeuristics(string value)
        {
            return DefaultBotKeywords.Any(keyword => value.Contains(keyword));
        }

        private static bool MatchesAiAgentHeuristics(string name, string email)
        {
            string emailLower = email.ToLowerInvariant();
            string nameLower = name.Trim().ToLowerInvariant();
            return emailLower.Contains("@ampcode.com") ||
                   emailLower.StartsWith("gemini-cli@") ||
                   nameLower == "amp" ||
                   nameLower == "gemini cli";
        }

        private static bool MatchesConfiguredBotRules(
            GitIdentity identity,
            List<BotRule> configuredBots,
            string value)
        {
            return configuredBots.Any(bot =>
            {
                bool nameMatches = bot.Name != null && identity.Name.Equals(bot.Name, StringComparison.OrdinalIgnoreCase);
                bool emailMatches = bot.Email != null && identity.Email.Equals(bot.Email, StringComparison.OrdinalIgnoreCase);
                bool patternMatches = bot.Pattern != null && PathUtils.MatchesTextPattern(value, bot.Pattern.ToLowerInvariant());
                return nameMatches || emailMatches || patternMatches;
            });
        }

        public static bool IsBotIdentity(GitIdentity identity, List<BotRule> configuredBots)
        {
            string value = $"{identity.Name} {identity.Email}".ToLowerInvariant();
            return MatchesDefaultBotHeuristics(value) ||
                   MatchesAiAgentHeuristics(identity.Name, identity.Email) ||
                   MatchesConfiguredBotRules(identity, configuredBots, value);
        }

        public static bool IsGithubNoreply(string email)
        {
            return email.EndsWith(GithubNoreplySuffix, StringComparison.OrdinalIgnoreCase);
        }

        public static string ParseNoreplyUsername(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return string.Empty;
            }

            int atIndex = email.IndexOf('@');
            string local = atIndex >= 0 ? email[..atIndex] : email;

            int plusIndex = local.LastIndexOf('+');
            string username = plusIndex >= 0 ? local[(plusIndex + 1)..] : local;

            return username.ToLowerInvariant();
        }
    }

    public interface IIdentityRegistry
    {
        void RegisterRealIdentity(GitIdentity identity);
        GitIdentity Resolve(GitIdentity identity);
        List<EmailCollision> GetEmailCollisions();
        bool IsBot(GitIdentity identity);
        string IdentityKey(GitIdentity identity);
        string IdentityKey(string name, string email);
        GitIdentity ResolveAlias(GitIdentity identity);
        bool IsGithubNoreply(string email);
        string ParseNoreplyUsername(string email);
        bool SameIdentity(GitIdentity left, GitIdentity right);
    }

    public class IdentityRegistry : IIdentityRegistry
    {
        private readonly List<AliasRule> _aliases;
        private readonly List<BotRule> _bots;
        private readonly bool _mergeOnEmail;
        private readonly IIdentityKeyGenerator _keyGenerator;
        private readonly Dictionary<string, GitIdentity> _emailCanonical = new();
        private readonly Dictionary<string, Dictionary<string, string>> _rawEmailNames = new();
        private readonly Dictionary<string, GitIdentity> _nameToRealCanonical = new();

        internal IdentityRegistry(List<AliasRule>? aliases = null, List<BotRule>? bots = null, bool mergeOnEmail = false, IIdentityKeyGenerator? keyGenerator = null)
        {
            _aliases = aliases ?? [];
            _bots = bots ?? [];
            _mergeOnEmail = mergeOnEmail;
            _keyGenerator = keyGenerator ?? IdentityKeyGenerator.Default;
        }

        public void RegisterRealIdentity(GitIdentity identity)
        {
            if (!IsGithubNoreply(identity.Email))
            {
                string key = identity.Name.ToLowerInvariant();
                if (!_nameToRealCanonical.ContainsKey(key))
                {
                    _nameToRealCanonical[key] = identity;
                }
            }
        }

        public GitIdentity Resolve(GitIdentity identity)
        {
            string emailLower = identity.Email.ToLowerInvariant();
            string nameLower = identity.Name.ToLowerInvariant();

            if (!_rawEmailNames.TryGetValue(emailLower, out var nameMap))
            {
                nameMap = new Dictionary<string, string>();
                _rawEmailNames[emailLower] = nameMap;
            }
            if (!nameMap.ContainsKey(nameLower))
            {
                nameMap[nameLower] = identity.Name;
            }

            var aliased = ResolveAlias(identity);
            if (!SameIdentity(aliased, identity))
            {
                return aliased;
            }

            if (_mergeOnEmail)
            {
                string mergeKey;
                if (IsGithubNoreply(identity.Email))
                {
                    string username = ParseNoreplyUsername(identity.Email);
                    if (_nameToRealCanonical.TryGetValue(username, out var real))
                    {
                        mergeKey = real.Email.ToLowerInvariant();
                    }
                    else
                    {
                        mergeKey = $"gh:{username}";
                    }
                }
                else
                {
                    mergeKey = emailLower;
                }

                if (_emailCanonical.TryGetValue(mergeKey, out var canonical))
                {
                    return canonical;
                }

                _emailCanonical[mergeKey] = identity;
                return identity;
            }

            return aliased;
        }

        public List<EmailCollision> GetEmailCollisions()
        {
            var collisions = new List<EmailCollision>();
            var sortedEmails = _rawEmailNames.Keys.OrderBy(e => e).ToList();
            foreach (var email in sortedEmails)
            {
                var nameMap = _rawEmailNames[email];
                if (nameMap.Count >= 2)
                {
                    collisions.Add(new EmailCollision
                    {
                        Email = email,
                        Names = nameMap.Values.OrderBy(n => n).ToList()
                    });
                }
            }
            return collisions;
        }

        public bool IsBot(GitIdentity identity)
        {
            return IdentityUtils.IsBotIdentity(identity, _bots);
        }

        public string IdentityKey(GitIdentity identity)
        {
            return _keyGenerator.IdentityKey(identity);
        }

        public string IdentityKey(string name, string email)
        {
            return _keyGenerator.IdentityKey(name, email);
        }

        public GitIdentity ResolveAlias(GitIdentity identity)
        {
            return IdentityUtils.ResolveAlias(identity, _aliases);
        }

        public bool IsGithubNoreply(string email)
        {
            return IdentityUtils.IsGithubNoreply(email);
        }

        public string ParseNoreplyUsername(string email)
        {
            return IdentityUtils.ParseNoreplyUsername(email);
        }

        public bool SameIdentity(GitIdentity left, GitIdentity right)
        {
            return IdentityUtils.SameIdentity(left, right);
        }
    }

    public class EmailCollision
    {
        public string Email { get; set; } = string.Empty;
        public List<string> Names { get; set; } = new();
    }
}
