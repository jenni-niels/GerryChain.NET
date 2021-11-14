using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace GerryChain
{
    public record Proposal(Partition Partition, (int, int) DistrictsAffected, Dictionary<int, int> Flips);
    public class ReComChain: IEnumerable<Partition>
    {
        public Partition InitialPartition { get; private set; }
        public int RngSeed { get; private set; }
        private Random rng;
        public int MaxSteps { get; private set; }
        public int MaxDegreeOfParallelism { get; private set; }

        /// <summary>
        /// Constraints are encoded in the acceptance.
        /// </summary>
        public Func<Partition, double> AcceptanceFunction { get; private set; }
        public double EpsilonBalance { get; private set; }

        public ReComChain(Partition initialPartition, int numSteps, double epsilon, int randomSeed = 0,
                          int degreeeOfParallelism = 1, Func<Partition, double> accept = null)
        {
            InitialPartition = initialPartition;
            MaxSteps = numSteps;
            EpsilonBalance = epsilon;
            RngSeed = randomSeed;
            MaxDegreeOfParallelism = degreeeOfParallelism;
            AcceptanceFunction = (accept is null) ? _ => 1.0 : accept;

            rng = new Random(RngSeed);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
        return GetEnumerator();
        }

        public IEnumerator<Partition> GetEnumerator()
        {
            return new ReComChainEnumerator(this);
        }

        public class ReComChainEnumerator: IEnumerator<Partition>
        {
            private ReComChain chain;
            private int step;
            private Partition currentPartition;

            public ReComChainEnumerator(ReComChain chainDetails)
            {
                chain = chainDetails;
                step = -1;
                currentPartition = null;
            }

            public bool MoveNext()
            {
                step++;
                if (step >= chain.MaxSteps )
                {
                    return false;
                }
                else if (step == 0) 
                {
                    currentPartition = chain.InitialPartition;
                }
                else 
                {
                    /// TODO:: implement next step.
                }
                return true;
            }
            public void Reset() { step = -1; }
            void IDisposable.Dispose() { }
            public Partition Current
            {
                get { return currentPartition; }
            }
            object IEnumerator.Current
            {
                get { return Current; }
            }

        }

        private void abc() {
            _ = Enumerable.Range(0, 20).AsParallel().WithDegreeOfParallelism(MaxDegreeOfParallelism);
        }
    }
}
