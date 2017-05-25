using Measurement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public interface IDomainResolver
    {
        Domain GetDomain(string domainName);
    }

    public class DomainResolver : IDomainResolver
    {
        protected readonly List<Domain> domains;

        public DomainResolver(IEnumerable<Domain> domains)
        {
            if (domains == null) throw new ArgumentNullException("domains");
            this.domains = domains.ToList();
        }

        public Domain GetDomain(string domainName)
        {
            if (domainName == null) throw new ArgumentNullException("domainName");
            foreach (var d in domains)
            {
                if(d.Name == domainName)
                {
                    return d;
                }
            }
            throw new KeyNotFoundException(String.Format("Domain '{0}' not found", domainName));
        }
    }
}
