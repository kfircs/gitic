import re

with open("src/Anonymizer.cs", "r") as f:
    content = f.read()

# Add interface and class
new_classes = """    public interface IIdentityAnonymizationCache
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
    }"""

# Remove old AnonymizationSession
content = re.sub(r'    internal class AnonymizationSession.*?    }', '', content, flags=re.DOTALL)

# Insert the new classes before ResultAnonymizer
content = content.replace("    public class ResultAnonymizer : IResultAnonymizer", new_classes + "\n\n    public class ResultAnonymizer : IResultAnonymizer")

# Remove constants, GetOrAnonymize, AnonymizeHuman, AnonymizeAutomation from ResultAnonymizer
# We'll just replace the whole chunk inside ResultAnonymizer from start to the first AnonymizeHumanContributorShare

chunk_to_remove_regex = r'        private const string ContributorNamePrefix.*?        private ContributorShare AnonymizeHumanContributorShare'
# Actually let's use a simpler way.
content = re.sub(
    r'        private const string ContributorNamePrefix = "Contributor";\s+private const string ContributorEmailPrefix = "contributor";\s+private const string AutomationNamePrefix = "Automation";\s+private const string AutomationEmailPrefix = "automation";\s+public ResultAnonymizer\(\)\s+\{\s+\}\s+private GitIdentity GetOrAnonymize.*?private ContributorShare AnonymizeHumanContributorShare',
    '        public ResultAnonymizer()\n        {\n        }\n\n        private ContributorShare AnonymizeHumanContributorShare',
    content,
    flags=re.DOTALL
)

# Now replace session type with IIdentityAnonymizationCache
content = content.replace("AnonymizationSession session", "IIdentityAnonymizationCache cache")
content = content.replace("AnonymizeHuman(contributor.Name, contributor.Email, session)", "cache.AnonymizeHuman(contributor.Name, contributor.Email)")
content = content.replace("AnonymizeAutomation(automation.Name, automation.Email, session)", "cache.AnonymizeAutomation(automation.Name, automation.Email)")

content = content.replace("AnonymizeHumanContributorShare(contributor, session)", "AnonymizeHumanContributorShare(contributor, cache)")
content = content.replace("AnonymizeFileMetric(file, session)", "AnonymizeFileMetric(file, cache)")
content = content.replace("AnonymizeAreaMetric(area, session)", "AnonymizeAreaMetric(area, cache)")
content = content.replace("AnonymizeHumanContributorMetric(contributor, session)", "AnonymizeHumanContributorMetric(contributor, cache)")

content = content.replace("var session = new AnonymizationSession();", "var cache = new IdentityAnonymizationCache();")

with open("src/Anonymizer.cs", "w") as f:
    f.write(content)

