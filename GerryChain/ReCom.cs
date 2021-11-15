using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using QuikGraph;
using QuikGraph.Algorithms.MinimumSpanningTree;
using QuikGraph.Algorithms.Observers;
using QuikGraph.Algorithms.Search;

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
        private double idealPopulation;
        private double minimumValidPopulation;
        private double maximumValidPopulation;
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
            idealPopulation = InitialPartition.Graph.TotalPop / InitialPartition.NumDistricts;
            minimumValidPopulation = idealPopulation * (1 - epsilon);
            maximumValidPopulation = idealPopulation * (1 + epsilon);
        }

        /// <summary>
        /// Helper function to hash edges by a long rather than the class type.
        /// </summary>
        /// <param name="e">edge to hash</param>
        /// <returns>ulong hash of edge</returns>
        /// TODO:: see if this can be replace by giving each edge and index.
        private static long EdgeHash(IUndirectedEdge<int> e)
        {
            return (long)e.Source << 32 ^ (int)e.Target;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentPartition"></param>
        /// <param name="randomSeed"></param>
        /// <returns></returns>
        /// TODO:: add county aware option, where the edges are tagged with whether they cross county
        /// bounds and that 
        private Proposal SampleProposalViaCutEdge(Partition currentPartition, int randomSeed)
        {
            Random generatorRNG = new Random(randomSeed);
            IUndirectedEdge<int> cutedge = currentPartition.CutEdges.ElementAt(generatorRNG.Next(currentPartition.CutEdges.Count()));
            int[] districts = { cutedge.Source, cutedge.Target };
            var subgraph = currentPartition.DistrictSubGraph(districts.ToHashSet());
            UndirectedGraph<int, IUndirectedEdge<int>> mst = MST(generatorRNG, subgraph);

            /// TODO:: Balanced edge
            var flips = FindBalancedCut(generatorRNG, mst, (cutedge.Source, cutedge.Target));

            return flips is null ? null : new Proposal(currentPartition, (cutedge.Source, cutedge.Target), flips);
        }

        private UndirectedGraph<int, IUndirectedEdge<int>> MST(Random generatorRNG, UndirectedGraph<int, IUndirectedEdge<int>> subgraph)
        {
            var edgeWeights = new Dictionary<long, double>();

            foreach (IUndirectedEdge<int> edge in subgraph.Edges)
                // TODO:: add if CountyAware condition
                edgeWeights[EdgeHash(edge)] = generatorRNG.NextDouble();

            var kruskal = new KruskalMinimumSpanningTreeAlgorithm<int, IUndirectedEdge<int>>(subgraph, e => edgeWeights[EdgeHash(e)]);

            var edgeRecorder = new EdgeRecorderObserver<int, IUndirectedEdge<int>>();
            using (edgeRecorder.Attach(kruskal))
                kruskal.Compute();

            return edgeRecorder.Edges.ToUndirectedGraph<int, IUndirectedEdge<int>>();
        }

        private bool IsValidPopulation(double population)
        {
            return population >= minimumValidPopulation && population <= maximumValidPopulation;
        }

        /// <summary>
        /// Using Contraction on the leaf nodes in the mst
        /// </summary>
        /// <param name="mst"></param>
        /// <returns></returns>
        /// TODO:: consider trades of using dictionary as space array vs. a sparsly used array.
        private Dictionary<int, int> FindBalancedCut(Random generatorRNG, UndirectedGraph<int, IUndirectedEdge<int>> mst, (int, int) districts)
        {
            int root = mst.Vertices.First(v => mst.AdjacentDegree(v) > 1);
            var leaves = new Queue<int>(mst.Vertices.Where(v => mst.AdjacentDegree(v) == 1));

            var nodePaths = mst.Vertices.ToDictionary(v => v,
                                                      v =>
                                                      {
                                                          var subset = new HashSet<int>();
                                                          subset.Add(v);
                                                          return subset;
                                                      });
            var nodePopulations = mst.Vertices.ToDictionary(v => v, v => InitialPartition.Graph.Populations[v]);

            var bfs = new UndirectedBreadthFirstSearchAlgorithm<int, IUndirectedEdge<int>>(mst);
            var nodePredecessorObserver = new UndirectedVertexPredecessorRecorderObserver<int, IUndirectedEdge<int>>();
            using (nodePredecessorObserver.Attach(bfs))
                bfs.Compute(root);

            var cuts = new List<int>();
            while (leaves.Count > 0)
            {
                int leaf = leaves.Dequeue();
                double leafPopulation = nodePopulations[leaf];
                if (IsValidPopulation(leafPopulation))
                {
                    cuts.Append(leaf);
                }
                int parent = nodePredecessorObserver.VerticesPredecessors[leaf].GetOtherVertex(leaf);

                /// Contract leaf and parent
                nodePopulations[parent] += leafPopulation;
                nodePaths[parent].UnionWith(nodePaths[leaf]);
                mst.RemoveVertex(leaf);

                if (mst.AdjacentDegree(parent) == 1 && parent != root)
                {
                    leaves.Enqueue(parent);
                }
            }

            if (cuts.Count == 0)
            {
                return null;
            }
            int cut = cuts.ElementAt(generatorRNG.Next(cuts.Count));
            HashSet<int> district1 = nodePaths[cut];
            HashSet<int> district2 = mst.Vertices.ToHashSet();
            district2.ExceptWith(district1);
            
            var flips = new Dictionary<int, int>();
            foreach (int node in district1)
            {
                flips[node] = districts.Item1;
            }
            foreach (int node in district2)
            {
                flips[node] = districts.Item2;
            }
            return flips;
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

                    IEnumerable<Proposal> proposals = seeds.Select(i => chain.SampleProposalViaCutEdge(currentPartition, randSeed + i));
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
    }
}
