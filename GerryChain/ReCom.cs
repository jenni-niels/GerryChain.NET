using System;
using System.Collections.Generic;

namespace GerryChain
{
    public record Proposal(Partition Part, (int, int) DistrictsAffected, Dictionary<int, int> Flips);
    
    public class ReComChain
    {
        public Partition CurrentPartion { get; private set; }
        public int Step { get; private set; }
        
        /// <summary>
        /// Constraints are encoded in the acceptance.
        /// </summary>
        public Func<Partition, double> AcceptanceFunction { get; private set; }
        public double EpsilonBalance { get; private set; }
    }
}