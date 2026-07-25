using System;
using System.Collections.Generic;
using System.Linq;

namespace Gitic
{
    public class ContributorNotFoundError : Exception
    {
        public ContributorNotFoundError(string message) : base(message)
        {
        }
    }

    public class AmbiguousContributorError : Exception
    {
        public AmbiguousContributorError(string message) : base(message)
        {
        }
    }

    public class ContributorLookupRegistry
    {
        private readonly List<ContributorMetric> _contributors;

        public ContributorLookupRegistry(List<ContributorMetric> contributors)
        {
            _contributors = contributors;
        }

        public ContributorMetric Find(string lookup)
        {
            var exact = _contributors.FirstOrDefault(c => c.Name == lookup);
            if (exact != null)
            {
                return exact;
            }

            string normalizedLookup = lookup.ToLower();
            var matches = _contributors.Where(c =>
                c.Name.ToLower() == normalizedLookup ||
                c.Email.ToLower() == normalizedLookup
            ).ToList();

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                string candidates = string.Join(", ", matches.Select(c => $"{c.Name} <{c.Email}>"));
                throw new AmbiguousContributorError(
                    $"Contributor lookup \"{lookup}\" is ambiguous. Candidates: {candidates}"
                );
            }

            throw new ContributorNotFoundError(
                $"Contributor \"{lookup}\" was not found in the selected analysis."
            );
        }
    }
}
