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
    /// <param name="NewDistrictPops"> The population of the new districts. </param>
    public record Proposal(Partition Partition, (int A, int B) DistrictsAffected, Dictionary<int, int[]> Flips, (double, double)? NewDistrictPops);
    
    /// <summary>
    /// Record encoding the information of a ReCom proposal, without the partion reference.
    /// </summary>
    /// <param name="DistrictsAffected"> The districts that were re-combined </param>
    /// <param name="Flips">The new district assignment </param>
    /// <param name="NewDistrictPops"> The population of the new districts. </param>
    public record ProposalSummary((int A, int B) DistrictsAffected, Dictionary<int, int[]> Flips, (double, double) NewDistrictPops);


    /// <summary>
    /// Class representing a ReCom chain
    /// </summary>
    /// <remarks>
    /// Inherits from IEnumerable<T> to support for each syntax and LINQ methods.
    /// </remarks>
    public class Chain : IEnumerable<Partition>
    {
        public Partition InitialPartition { get; private set; }
        public int RngSeed { get; private set; }
        public int MaxSteps { get; private set; }
        public int MaxDegreeOfParallelism { get; private set; }
        public int BatchSize { get; private set; }
        private readonly bool useDefaultParallelism = false;

        /// <summary>
        /// Function returning the propability of acceptance for a passed proposal and step.
        /// If the return value is always >= 1, the function always accepts; if the return values is
        /// always <= 0, the function always rejects.
        /// </summary>
        /// <remarks>
        /// Both hard and soft constraints can be applied to the chain here.
        /// </remarks>
        public Func<Partition, int, double> AcceptanceFunction { get; private set; }
        public double EpsilonBalance { get; private set; }
        private readonly double idealPopulation;

        protected ProposalGenerator ChainType { get; init; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="initialPartition"> Seed partition </param>
        /// <param name="numSteps"></param>
        /// <param name="epsilon"> Parameter setting how tightly to balance population. Districts are
        /// required to have population between (1 - \epsilon) * ideal population and 
        /// (1 + \epsilon) * ideal population. </param>
        /// <param name="randomSeed"> Value to seed the random number generator with. </param>
        /// <param name="accept"> The acceptance function to use in the Markov Chain. </param>
        /// <param name="degreeOfParallelism"> Maximum number of proposals generation tasks to execute
        /// in parallel. If 0, the default system behavior is used. </param>
        /// <param name="batchSize"> How many proposals to try to generate at each step. </param>
        /// <param name="frozenDistricts"> Set of district ids to "freeze" and not allow to change
        /// in the course of the chain. </param>
        public Chain(Partition initialPartition, int numSteps, double epsilon, int randomSeed = 0,
                     Func<Partition, int, double> accept = null, int degreeOfParallelism = 0, 
                     int batchSize = 32, HashSet<int> frozenDistricts = null, double? populationTarget = null)
        {
            InitialPartition = initialPartition;
            MaxSteps = numSteps;
            EpsilonBalance = epsilon;
            RngSeed = randomSeed;
            BatchSize = batchSize;
            AcceptanceFunction = (accept is null) ? (_, _) => 1.0 : accept;

            if (degreeOfParallelism < 1) { useDefaultParallelism = true; }
            else { MaxDegreeOfParallelism = degreeOfParallelism; }

            if (populationTarget is double populationTargetValue) { idealPopulation = populationTargetValue; }
            else { idealPopulation = InitialPartition.Graph.TotalPop / InitialPartition.NumDistricts; }

            if (InitialPartition.Graph.TotalPop > idealPopulation * InitialPartition.NumDistricts * (1 + epsilon) ||
                InitialPartition.Graph.TotalPop < idealPopulation * InitialPartition.NumDistricts * (1 - epsilon))
            {
                throw new ArgumentOutOfRangeException("populationTarget", populationTarget,
                                                      "The target population must be achievable within the range of the initial partition's: "
                                                      + $"total population ({InitialPartition.Graph.TotalPop}), "
                                                      + $"and number of districts ({InitialPartition.NumDistricts}) "
                                                      + $"with respect to the passed epsilon ({epsilon}).");
            }

            if (frozenDistricts is null) { frozenDistricts = new HashSet<int>(); }
            ChainType = new CutEdgeReComProposalGenerator(idealPopulation, epsilon, initialPartition.Graph, frozenDistricts);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Partition> GetEnumerator()
        {
            return new ChainEnumerator(this);
        }

        /// <summary>
        /// Nested sub-class defining the enumeration behavior of the chain and how it selects the
        /// next partition.
        /// 
        /// Only contains fields for the current partition, step, the RNG, and a reference to the
        /// instance of the outer class.
        /// </summary>
        public class ChainEnumerator : IEnumerator<Partition>
        {
            private readonly Chain chain;
            private int step;
            private Partition currentPartition;
            private Random rng;
            private Queue<Proposal> sampledValidProposals;

            public ChainEnumerator(Chain chainDetails)
            {
                chain = chainDetails;
                step = -1;
                currentPartition = null;
                rng = new Random(chain.RngSeed);
                sampledValidProposals = new Queue<Proposal>();
            }

            /// <summary>
            /// Check if proposal is accepted.  If accepted the new partition is returned otherwise
            /// a self loop is taken and the current partition is returned.
            /// </summary>
            /// <param name="proposal">Proposal to accept or reject.</param>
            /// <param name="extraProposals">Other valid proposals to be cached if a rejection
            /// / self loop occurs. </param>
            /// <returns>Next step's partiton</returns>
            private Partition CheckAcceptance(Proposal proposal, IEnumerable<Proposal> extraProposals = null)
            {
                Partition part = new Partition(proposal);
                bool accept = rng.NextDouble() < chain.AcceptanceFunction(part, step);
                if (accept)
                {
                    sampledValidProposals.Clear();
                    return part;
                }
                else
                {
                    if (extraProposals is not null)
                    {
                        foreach (Proposal p in extraProposals)
                        {
                            sampledValidProposals.Enqueue(p);
                        }
                    }
                    return currentPartition.TakeSelfLoop();
                }
            }
            /// <summary>
            /// Take a step in the ChainEnumerator
            /// </summary>
            /// <returns></returns>
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
                else if (sampledValidProposals.Count > 0)
                {
                    currentPartition = CheckAcceptance(sampledValidProposals.Dequeue());
                }
                else
                {
                    int randSeed = rng.Next();
                    ParallelQuery<int> seeds = chain.useDefaultParallelism ? Enumerable.Range(0, chain.BatchSize).AsParallel().AsOrdered()
                                                                           : Enumerable.Range(0, chain.BatchSize).AsParallel().AsOrdered()
                                                                                       .WithDegreeOfParallelism(chain.MaxDegreeOfParallelism);

                    ParallelQuery<Proposal> proposals = seeds.Select(i => chain.ChainType.SampleProposal(currentPartition, randSeed + i));
                    Proposal[] validProposals = proposals.Where(p => p is not null).ToArray();

                    if (validProposals.Length == 0)
                    {
                        currentPartition = currentPartition.TakeSelfLoop();
                    }
                    else
                    {
                        int proposalIndex = rng.Next(validProposals.Length);
                        currentPartition = CheckAcceptance(validProposals[proposalIndex],
                                                           extraProposals: validProposals.Where((p, i) => i != proposalIndex));
                    }
                }
                return true;
            }
            public void Reset()
            {
                step = -1;
                rng = new Random(chain.RngSeed);
                sampledValidProposals.Clear();
                currentPartition = null;
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

    /// <summary>
    /// Abstract class defining a ProposalGenerator.  Declares interface, common properties, and 
    /// population validity of proposal.
    /// </summary>
    public abstract class ProposalGenerator
    {
        protected double MinimumValidPopulation { get; init; }
        protected double MaximumValidPopulation { get; init; }
        protected DualGraph Graph { get; init; }
        
        /// <summary>
        /// Districts to not mutate in the proposal.
        /// </summary>
        protected HashSet<int> FrozenDistricts { get; init; }
        public abstract Proposal SampleProposal(Partition currentPartition, int randomSeed);
        protected bool IsValidPopulation(double population, double totalPopulation)
        {
            bool validDistrictA = population >= MinimumValidPopulation && population <= MaximumValidPopulation;
            double districtBPop = totalPopulation - population;
            bool validDistrictB = districtBPop >= MinimumValidPopulation && districtBPop <= MaximumValidPopulation;
            return validDistrictA && validDistrictB;
        }
    }

    /// <summary>
    /// Abstract class defining a ReComProposalGenerator.  Defines methods to sample a MST using
    /// graph's region divion penalities and to find a population balanced cut edge in a MST.
    /// </summary>
    public abstract class ReComProposalGenerator : ProposalGenerator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="generatorRNG"> The random number generator to use. </param>
        /// <param name="subgraph"> The subgraph to sample the minimum spaning tree for. </param>
        /// <returns></returns>
        protected UndirectedGraph<int, IUndirectedEdge<int>> SampleMinimumSpanningTree(Random generatorRNG, UndirectedGraph<int, IUndirectedEdge<int>> subgraph)
        {
            var edgeWeights = new Dictionary<long, double>();

            foreach (IUndirectedEdge<int> edge in subgraph.Edges)
            {
                var edgeHash = DualGraph.EdgeHash(edge);
                edgeWeights[edgeHash] = generatorRNG.NextDouble() + Graph.RegionDivisionPenalties[edgeHash];
            }
            var kruskal = new KruskalMinimumSpanningTreeAlgorithm<int, IUndirectedEdge<int>>(subgraph, e => edgeWeights[DualGraph.EdgeHash(e)]);

            var edgeRecorder = new EdgeRecorderObserver<int, IUndirectedEdge<int>>();
            using (edgeRecorder.Attach(kruskal))
                kruskal.Compute();

            return edgeRecorder.Edges.ToUndirectedGraph<int, IUndirectedEdge<int>>();
        }

        /// <summary>
        /// Find balanced cut edge using contraction on the leaf nodes in the mst find.
        /// </summary>
        /// <param name="mst">Minimum spanning tree of combined district</param>
        /// <param name="districts">The district ids that have been combined.</param>
        /// <returns> The new assignment of the districts and their populations </returns>
        /// TODO:: consider trades of using dictionary as space array vs. a sparsly used array.
        protected (Dictionary<int, int[]> flips, (int A, int B) districtsPops)? FindBalancedCut(Random generatorRNG, UndirectedGraph<int, IUndirectedEdge<int>> mst, (int A, int B) districts)
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
            var nodePopulations = mst.Vertices.ToDictionary(v => v, v => Graph.Populations[v]);
            double totalPopulation = nodePopulations.Values.Sum();

            var bfs = new UndirectedBreadthFirstSearchAlgorithm<int, IUndirectedEdge<int>>(mst);
            var nodePredecessorObserver = new UndirectedVertexPredecessorRecorderObserver<int, IUndirectedEdge<int>>();
            using (nodePredecessorObserver.Attach(bfs))
                bfs.Compute(root);

            (double Penalty, int Node, bool Initialized) maxCut = (0, 0, false);

            while (leaves.Count > 0)
            {
                int leaf = leaves.Dequeue();
                double leafPopulation = nodePopulations[leaf];
                if (IsValidPopulation(leafPopulation, totalPopulation))
                {
                    var edge = nodePredecessorObserver.VerticesPredecessors[leaf];
                    var edgeHash = DualGraph.EdgeHash(edge);
                    var edgePenalty = Graph.RegionDivisionPenalties[edgeHash] + generatorRNG.NextDouble();
                    if (edgePenalty >= maxCut.Penalty){
                        maxCut = (edgePenalty, leaf, true);
                    }
                }
                int parent = nodePredecessorObserver.VerticesPredecessors[leaf].GetOtherVertex(leaf);

                // Contract leaf and parent
                nodePopulations[parent] += leafPopulation;
                nodePaths[parent].UnionWith(nodePaths[leaf]);
                mst.RemoveVertex(leaf);

                if (mst.AdjacentDegree(parent) == 1 && parent != root)
                {
                    leaves.Enqueue(parent);
                }
            }

            if (! maxCut.Initialized)
            {
                return null;
            }
            int cut = maxCut.Node;
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
    }

    public class CutEdgeReComProposalGenerator : ReComProposalGenerator
    {
        public CutEdgeReComProposalGenerator(double idealPopulation, double epsilon, DualGraph graph, HashSet<int> frozenDistricts)
        {
            MinimumValidPopulation = idealPopulation * (1 - epsilon);
            MaximumValidPopulation = idealPopulation * (1 + epsilon);
            Graph = graph;
            FrozenDistricts = frozenDistricts;
        }

        /// <summary>
        /// Sample Recom Proposal using random cut edge
        /// </summary>
        /// <param name="currentPartition">The current partition state of the chain..</param>
        /// <param name="randomSeed">Task's RNG seed.</param>
        /// <returns></returns>
        public override Proposal SampleProposal(Partition currentPartition, int randomSeed)
        {
            Random generatorRNG = new Random(randomSeed);
            (int A, int B) districts;
            do
            {
                IUndirectedEdge<int> cutedge = currentPartition.CutEdges.ElementAt(generatorRNG.Next(currentPartition.CutEdges.Count()));
                districts = (currentPartition.Assignments[cutedge.Source], currentPartition.Assignments[cutedge.Target]);
            } while (FrozenDistricts.Contains(districts.A) || FrozenDistricts.Contains(districts.B));

            var subgraph = currentPartition.DistrictSubGraph(districts);
            
            UndirectedGraph<int, IUndirectedEdge<int>> mst = SampleMinimumSpanningTree(generatorRNG, subgraph);

            var balancedCut = FindBalancedCut(generatorRNG, mst, districts);

            if (balancedCut is (Dictionary<int, int[]>, (int, int) districtsPops) cut)
            {
                return new Proposal(currentPartition, districts, cut.flips, cut.districtsPops);
            }
            else
            {
                return null;
            }
        } 
    }
}
