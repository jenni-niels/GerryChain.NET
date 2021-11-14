using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using QuikGraph;
using QuikGraph.Algorithms.MinimumSpanningTree;
using QuikGraph.Algorithms.Observers;

namespace GerryChain
{
    public record Proposal(Partition Partition, (int, int) DistrictsAffected, Dictionary<int, int> Flips);
    public class ReComChain : IEnumerable<Partition>
    {
        public Partition InitialPartition { get; private set; }
        public int RngSeed { get; private set; }
        private Random rng;
        public int MaxSteps { get; private set; }
        public int MaxDegreeOfParallelism { get; private set; }
        public int BatchSize { get; private set; }
        private bool useDefaultParallelism = false;

        /// <summary>
        /// Constraints are encoded in the acceptance.
        /// </summary>
        public Func<Partition, double> AcceptanceFunction { get; private set; }
        public double EpsilonBalance { get; private set; }
        public bool CountyAware { get; private set; }

        public ReComChain(Partition initialPartition, int numSteps, double epsilon, int randomSeed = 0,
                        Func<Partition, double> accept = null, bool countyAware = false, int degreeeOfParallelism = 0,
                        int batchSize = 32)
        {
            InitialPartition = initialPartition;
            MaxSteps = numSteps;
            EpsilonBalance = epsilon;
            RngSeed = randomSeed;
            BatchSize = batchSize;
            AcceptanceFunction = (accept is null) ? _ => 1.0 : accept;
            CountyAware = countyAware;

            if (degreeeOfParallelism < 1) { useDefaultParallelism = true; }
            else { MaxDegreeOfParallelism = degreeeOfParallelism; }

            rng = new Random(RngSeed);
        }

        /// <summary>
        /// Helper function to hash edges by a long rather than the class type.
        /// </summary>
        /// <param name="e">edge to hash</param>
        /// <returns>ulong hash of edge</returns>
        /// TODO:: see if this can be replace by giving each edge and index.
        private static ulong EdgeHash(IUndirectedEdge<int> e)
        {
            return (ulong)e.Source << 32 | (uint)e.Target;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentPartition"></param>
        /// <param name="randomSeed"></param>
        /// <returns></returns>
        /// TODO:: add county aware option, where the edges are tagged with whether they cross county
        /// bounds and that 
        private Proposal SampleProposalCutEdge(Partition currentPartition, int randomSeed)
        {
            Random generatorRNG = new Random(randomSeed);
            IUndirectedEdge<int> cutedge = currentPartition.CutEdges.ElementAt(generatorRNG.Next(currentPartition.CutEdges.Count()));
            int[] districts = { cutedge.Source, cutedge.Target };
            var subgraph = currentPartition.DistrictSubGraph(districts.ToHashSet());
            var flips = new Dictionary<int, int>();
            IEnumerable<IUndirectedEdge<int>> mst = MST(generatorRNG, subgraph);

            /// TODO:: Balanced edge

            return new Proposal(currentPartition, (cutedge.Source, cutedge.Target), flips);
        }

        private IEnumerable<IUndirectedEdge<int>> MST(Random generatorRNG, UndirectedGraph<int, IUndirectedEdge<int>> subgraph)
        {
            var edgeWeights = new Dictionary<ulong, double>();

            foreach (IUndirectedEdge<int> edge in subgraph.Edges)
                // TODO:: add if CountyAware condition
                edgeWeights[EdgeHash(edge)] = generatorRNG.NextDouble();

            var kruskal = new KruskalMinimumSpanningTreeAlgorithm<int, IUndirectedEdge<int>>(subgraph, e => edgeWeights[EdgeHash(e)]);

            var edgeRecorder = new EdgeRecorderObserver<int, IUndirectedEdge<int>>();
            using (edgeRecorder.Attach(kruskal))
                kruskal.Compute();

            return edgeRecorder.Edges;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Partition> GetEnumerator()
        {
            return new ReComChainEnumerator(this);
        }

        public class ReComChainEnumerator : IEnumerator<Partition>
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
                if (step >= chain.MaxSteps)
                {
                    return false;
                }
                else if (step == 0)
                {
                    currentPartition = chain.InitialPartition;
                }
                else
                {
                    int randSeed = chain.rng.Next();
                    IEnumerable<int> seeds = chain.useDefaultParallelism ? Enumerable.Range(0, chain.BatchSize).AsParallel()
                                                                         : Enumerable.Range(0, chain.BatchSize).AsParallel()
                                                                                     .WithDegreeOfParallelism(chain.MaxDegreeOfParallelism);

                    IEnumerable<Proposal> proposals = seeds.Select(i => chain.SampleProposalCutEdge(currentPartition, randSeed + i));
                    IEnumerable<Proposal> validProposals = proposals.Where(p => p is not null);

                    currentPartition = validProposals.Count() switch
                    {
                        0 => currentPartition.TakeSelfLoop(),
                        > 0 => new Partition(validProposals.ElementAt(chain.rng.Next(validProposals.Count()))),
                        _ => throw new IndexOutOfRangeException("Length of valid proposals should not be negative!")
                    };
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

        private void abc()
        {
            _ = Enumerable.Range(0, 20).AsParallel().WithDegreeOfParallelism(MaxDegreeOfParallelism);
        }
    }
}
