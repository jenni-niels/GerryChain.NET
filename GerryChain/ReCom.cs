using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using QuikGraph.Algorithms.MinimumSpanningTree;
using QuikGraph.Algorithms.Observers;
using QuikGraph.Algorithms.Search;

namespace GerryChain
{
    /// <summary>
    /// Record encoding the information of a ReCom proposal.
    /// </summary>
    /// <param name="Partition">Original Partion</param>
    /// <param name="DistrictsAffected"> The districts that were re-combined </param>
    /// <param name="Flips">The new district assignment </param>
    /// <param name="newDistrictPops"> The population of the new districts. </param>
    public record ReComProposal(Partition Partition, (int A, int B) DistrictsAffected, Dictionary<int, int[]> Flips, (double, double) NewDistrictPops);
    public record ReComProposalSummary((int A, int B) DistrictsAffected, Dictionary<int, int[]> Flips, (double, double) NewDistrictPops);


    /// <summary>
    /// Class representing a ReCom chain
    /// </summary>
    /// <remarks>
    /// Inherits from IEnumerable<T> to support for each syntax and LINQ methods.
    /// </remarks>
    public class ReComChain : IEnumerable<Partition>
    {
        public Partition InitialPartition { get; private set; }
        public int RngSeed { get; private set; }
        public int MaxSteps { get; private set; }
        public int MaxDegreeOfParallelism { get; private set; }
        public int BatchSize { get; private set; }
        private bool useDefaultParallelism = false;

        /// <summary>
        /// Constraints are encoded in the acceptance.
        /// </summary>
        public Func<Partition, double> AcceptanceFunction { get; private set; }
        public double EpsilonBalance { get; private set; }
        private readonly double idealPopulation;
        private readonly double minimumValidPopulation;
        private readonly double maximumValidPopulation;
        public bool CountyAware { get; private set; }

        public ReComChain(Partition initialPartition, int numSteps, double epsilon, int randomSeed = 0,
                        Func<Partition, double> accept = null, bool countyAware = false, int degreeOfParallelism = 0,
                        int batchSize = 32)
        {
            InitialPartition = initialPartition;
            MaxSteps = numSteps;
            EpsilonBalance = epsilon;
            RngSeed = randomSeed;
            BatchSize = batchSize;
            AcceptanceFunction = (accept is null) ? _ => 1.0 : accept;
            CountyAware = countyAware;

            if (degreeOfParallelism < 1) { useDefaultParallelism = true; }
            else { MaxDegreeOfParallelism = degreeOfParallelism; }

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
        private static long EdgeHash(STaggedUndirectedEdge<int, EdgeTag> e)
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
        private ReComProposal SampleProposalViaCutEdge(Partition currentPartition, int randomSeed)
        {
            Random generatorRNG = new Random(randomSeed);
            STaggedUndirectedEdge<int, EdgeTag> cutedge = currentPartition.CutEdges.ElementAt(generatorRNG.Next(currentPartition.CutEdges.Count()));
            (int A, int B) districts = (currentPartition.Assignments[cutedge.Source], currentPartition.Assignments[cutedge.Target] );
            var subgraph = currentPartition.DistrictSubGraph(districts);
            
            UndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>> mst = MST(generatorRNG, subgraph);

            var balancedCut = FindBalancedCut(generatorRNG, mst, districts);

            if (balancedCut is (Dictionary<int, int[]> flips, (int A, int B) districtsPops) cut)
            {
                return new ReComProposal(currentPartition, districts, cut.flips, cut.districtsPops);
            }
            else
            {
                return null;
            }
        }

        private UndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>> MST(Random generatorRNG, UndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>> subgraph)
        {
            var edgeWeights = new Dictionary<long, double>();

            foreach (STaggedUndirectedEdge<int, EdgeTag> edge in subgraph.Edges)
                // TODO:: add if CountyAware condition
                edgeWeights[EdgeHash(edge)] = generatorRNG.NextDouble() + edge.Tag.RegionDivisionPenalty;

            var kruskal = new KruskalMinimumSpanningTreeAlgorithm<int, STaggedUndirectedEdge<int, EdgeTag>>(subgraph, e => edgeWeights[EdgeHash(e)]);

            var edgeRecorder = new EdgeRecorderObserver<int, STaggedUndirectedEdge<int, EdgeTag>>();
            using (edgeRecorder.Attach(kruskal))
                kruskal.Compute();

            return edgeRecorder.Edges.ToUndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>>();
        }

        private bool IsValidPopulation(double population, double totalPopulation)
        {
            bool validDistrictA = population >= minimumValidPopulation && population <= maximumValidPopulation;
            double districtBPop = totalPopulation - population;
            bool validDistrictB = districtBPop >= minimumValidPopulation && districtBPop <= maximumValidPopulation;
            return validDistrictA && validDistrictB;
        }

        /// <summary>
        /// Using Contraction on the leaf nodes in the mst
        /// </summary>
        /// <param name="mst"></param>
        /// <returns></returns>
        /// TODO:: consider trades of using dictionary as space array vs. a sparsly used array.
        private (Dictionary<int, int[]> flips, (int A, int B) districtsPops)? FindBalancedCut(Random generatorRNG, UndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>> mst, (int A, int B) districts)
        {
            int root = mst.Vertices.First(v => mst.AdjacentDegree(v) > 1);
            var leaves = new Queue<int>(mst.Vertices.Where(v => mst.AdjacentDegree(v) == 1));

            var nodesUniverse = new HashSet<int>(mst.Vertices);
            var nodePaths = mst.Vertices.ToDictionary(v => v,
                                                      v =>
                                                      {
                                                          var successors = new HashSet<int>();
                                                          successors.Add(v);
                                                          return successors;
                                                      });
            var nodePopulations = mst.Vertices.ToDictionary(v => v, v => InitialPartition.Graph.Populations[v]);
            double totalPopulation = nodePopulations.Values.Sum();

            var bfs = new UndirectedBreadthFirstSearchAlgorithm<int, STaggedUndirectedEdge<int, EdgeTag>>(mst);
            var nodePredecessorObserver = new UndirectedVertexPredecessorRecorderObserver<int, STaggedUndirectedEdge<int, EdgeTag>>();
            using (nodePredecessorObserver.Attach(bfs))
                bfs.Compute(root);

            var cuts = new List<int>();
            while (leaves.Count > 0)
            {
                int leaf = leaves.Dequeue();
                double leafPopulation = nodePopulations[leaf];
                if (IsValidPopulation(leafPopulation, totalPopulation))
                {
                    cuts.Add(leaf);
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
            double districtAPopulation = nodePopulations[cut];
            double districtBPopulation = totalPopulation - districtAPopulation;
            HashSet<int> districtA = nodePaths[cut];
            HashSet<int> districtB = nodesUniverse;
            districtB.ExceptWith(districtA);
            
            var flips = new Dictionary<int, int[]>();
            flips[districts.A] = districtA.ToArray();
            flips[districts.B] = districtB.ToArray();
            return ((Dictionary<int, int[]> flips, (int A, int B) districtsPops)?) (flips, (districtAPopulation, districtBPopulation));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Partition> GetEnumerator()
        {
            return new ReComChainEnumerator(this);
        }

        /// <summary>
        /// Nested sub-class defining the enumeration behavior of the chain and how it selects the
        /// next partition.
        /// 
        /// Only contains fields for the current partition, step, the RNG, and a reference to the
        /// instance of the outer class.
        /// </summary>
        public class ReComChainEnumerator : IEnumerator<Partition>
        {
            private readonly ReComChain chain;
            private int step;
            private Partition currentPartition;
            private Random rng;

            public ReComChainEnumerator(ReComChain chainDetails)
            {
                chain = chainDetails;
                step = -1;
                currentPartition = null;
                rng = new Random(chain.RngSeed);
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
                    int randSeed = rng.Next();
                    ParallelQuery<int> seeds = chain.useDefaultParallelism ? Enumerable.Range(0, chain.BatchSize).AsParallel()
                                                                           : Enumerable.Range(0, chain.BatchSize).AsParallel()
                                                                                       .WithDegreeOfParallelism(chain.MaxDegreeOfParallelism);

                    ParallelQuery<ReComProposal> proposals = seeds.Select(i => chain.SampleProposalViaCutEdge(currentPartition, randSeed + i));
                    ReComProposal[] validProposals = proposals.Where(p => p is not null).ToArray();

                    // foreach (ReComProposal p in validProposals)
                    // {
                    //     Console.WriteLine(p.NewDistrictPops.ToString());
                    // }

                    currentPartition = validProposals.Length switch
                    {
                        0 => currentPartition.TakeSelfLoop(),
                        > 0 => new Partition(validProposals[rng.Next(validProposals.Length)]),
                        _ => throw new IndexOutOfRangeException("Length of valid proposals should not be negative!")
                    };
                }
                return true;
            }
            public void Reset()
            {
                step = -1;
                rng = new Random(chain.RngSeed);
            }
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
