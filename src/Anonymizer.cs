using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Gitic
{
    public interface IResultAnonymizer
    {
        AnalysisResult Anonymize(AnalysisResult result);
    }

    public interface IIdentityAnonymizationCache
    {
        GitIdentity AnonymizeHuman(string name, string email);
        GitIdentity AnonymizeAutomation(string name, string email);
    }

    public class IdentityAnonymizationCache : IIdentityAnonymizationCache
    {
        private const string ContributorNamePrefix = "Contributor";
        private const string ContributorEmailPrefix = "contributor";
        private const string AutomationNamePrefix = "Automation";
        private const string AutomationEmailPrefix = "automation";

        private readonly Dictionary<string, GitIdentity> _humanIdentities = new();
        private readonly Dictionary<string, GitIdentity> _automationIdentities = new();

        private GitIdentity GetOrAnonymize(string name, string email, Dictionary<string, GitIdentity> cache, string namePrefix, string emailPrefix)
        {
            string key = IdentityUtils.IdentityKey(name, email);
            if (cache.TryGetValue(key, out var existing))
            {
                return existing;
            }
            int index = cache.Count + 1;
            var identity = new GitIdentity
            {
                Name = $"{namePrefix} {index}",
                Email = $"{emailPrefix}-{index}@anonymous.local"
            };
            cache[key] = identity;
            return identity;
        }

        public GitIdentity AnonymizeHuman(string name, string email)
        {
            return GetOrAnonymize(name, email, _humanIdentities, ContributorNamePrefix, ContributorEmailPrefix);
        }

        public GitIdentity AnonymizeAutomation(string name, string email)
        {
            return GetOrAnonymize(name, email, _automationIdentities, AutomationNamePrefix, AutomationEmailPrefix);
        }
    }

    public class ResultAnonymizer : IResultAnonymizer
    {
        public AnalysisResult Anonymize(AnalysisResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var serialized = JsonSerializer.Serialize(result);
            var cloned = JsonSerializer.Deserialize<AnalysisResult>(serialized) 
                         ?? throw new InvalidOperationException("Failed to clone AnalysisResult via JSON serialization.");

            var cache = new IdentityAnonymizationCache();

            // Anonymize human contributors
            AnonymizeList(cloned.Contributors, cache.AnonymizeHuman);

            // Anonymize automation contributors
            AnonymizeList(cloned.Automation, cache.AnonymizeAutomation);

            // Anonymize contributors in file metrics
            if (cloned.Files != null)
            {
                foreach (var file in cloned.Files)
                {
                    AnonymizeList(file.Contributors, cache.AnonymizeHuman);
                }
            }

            // Anonymize contributors in area metrics
            if (cloned.Areas != null)
            {
                foreach (var area in cloned.Areas)
                {
                    AnonymizeList(area.Contributors, cache.AnonymizeHuman);
                }
            }

            return cloned;
        }

        private static void AnonymizeList<T>(List<T>? list, Func<string, string, GitIdentity> anonymizeFunc) where T : IContributorIdentity
        {
            if (list == null) return;
            foreach (var item in list)
            {
                var identity = anonymizeFunc(item.Name, item.Email);
                item.Name = identity.Name;
                item.Email = identity.Email;
            }
        }
    }
}
