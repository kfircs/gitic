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

    public interface IContributorLookupRegistry
    {
        ContributorMetric Find(string lookup);
    }

    public class ContributorLookupRegistry : IContributorLookupRegistry
    {
        private readonly List<ContributorMetric> _contributors;

        public ContributorLookupRegistry(List<ContributorMetric> contributors)
        {
            _contributors = contributors;
        }

        public ContributorMetric Find(string lookup)
        {
            var exact = _contributors.FirstOrDefault(c => string.Equals(c.Name, lookup, StringComparison.Ordinal));
            if (exact != null)
            {
                return exact;
            }

            var matches = _contributors.Where(c =>
                string.Equals(c.Name, lookup, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Email, lookup, StringComparison.OrdinalIgnoreCase)
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
