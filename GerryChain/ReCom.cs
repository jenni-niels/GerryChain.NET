using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace GerryChain
{
    public record Proposal(Partition Partition, (int, int) DistrictsAffected, Dictionary<int, int> Flips);
    public class ReComChain
    {
        public Partition CurrentPartion { get; private set; }
        public int Step { get; private set; }
        public int RngSeed { get; private set; }

        public int MaxDegreeOfParallelism { get; private set; }

        private Random rng;

        /// <summary>
        /// Constraints are encoded in the acceptance.
        /// </summary>
        public Func<Partition, double> AcceptanceFunction { get; private set; }
        public double EpsilonBalance { get; private set; }

        public ReComChain() {
            rng = new Random(RngSeed);
        }


        private void abc() {
            _ = Enumerable.Range(0, 20).AsParallel().WithDegreeOfParallelism(MaxDegreeOfParallelism);
        }
    }
}
