using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuikGraph;

namespace GerryChain
{

    /// <summary>
    /// Represents an partition (or district assignment) on a graph.
    /// </summary>
    /// <member name="graph">
    /// The underlying graph
    /// </member>
    /// <member name="assignments">
    /// The districts assignments on the nodes
    /// </member>
    /// <member name="parentAssignments">
    /// The districts assignments on the nodes for the parent partition
    /// </member>
    public class Partition
    {
        public DualGraph Graph { get; private set; }
        public int[] Assignments { get; private set; }
        public bool HasParent { get; private set; }
        public int NumDistricts { get; private set; }
        public int[] ParentAssignments { get; private set; }
        public ReComProposalSummary ProposalSummary { get; private set; }
        public int SelfLoops { get; private set; } = 0;

        public IEnumerable<STaggedUndirectedEdge<int, EdgeTag>> CutEdges { get; private set; }

        private Dictionary<string, Score> ScoreFunctions { get; set; }
        private Dictionary<string, ScoreValue> ScoreValues { get; set; }
        private Dictionary<string, ScoreValue> ParentScoreValues { get; set; }

        /// <summary>
        /// Generate initial partition on dualgraph.
        /// </summary>
        /// <param name="graph"> Underlying Dual Graph </param>
        /// <param name="assignment"> Partition assignment on nodes of graph. </param>
        public Partition(DualGraph graph, int[] assignment, IEnumerable<Score> Scores)
        {
            HasParent = false;
            Graph = graph;
            ScoreFunctions = Scores.ToDictionary(s => s.Name);
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentScoreValues = new Dictionary<string, ScoreValue>();
            CutEdges = Graph.Graph.Edges.Where(e => Assignments[e.Source] != Assignments[e.Target]);
            bool oneIndexed = (assignment.Min() == 1);
            Assignments = oneIndexed ? assignment.Select(d => d - 1).ToArray() : assignment;
            NumDistricts = oneIndexed ? assignment.Max() : assignment.Max() + 1;
        }

        /// <summary>
        /// Create a DualGraph representation for a state given a networkx json representation.
        /// </summary>
        /// <param name="jsonFilePath"> path to networkx json file </param>
        /// <param name="assignmentColumn"> Column name in the json file that contains the initial
        /// assignment.  The values of the assignment column must be 0 or 1 indexed.</param>
        /// <param name="populationColumn"> Column name in the json file that contains the population of each node. </param>
        /// <param name="columnsToTrack"> names of columns tracts as attributes </param>
        /// <returns> New instance of DualGraph record </returns>
        /// <remarks> Nodes are assumed to be indexed from 0 .. n-1 </remarks>
        public Partition(string jsonFilePath, string assignmentColumn, string populationColumn, string[] columnsToTrack,
                         IEnumerable<Score> Scores, bool regionAware = false, (string, double)[] regionDivisionSpecs = null)
        {
            if (regionAware && regionDivisionSpecs is null)
            {
                throw new ArgumentException("Cannot create region aware graph without region specification.");
            }

            double[] populations;
            int[] assignments;
            var regions = new Dictionary<string, (double penalty, int[] mappings)>();
            IEnumerable<STaggedUndirectedEdge<int, EdgeTag>> edges;
            var attributes = new Dictionary<string, double[]>();

            using (StreamReader reader = File.OpenText(jsonFilePath))
            {
                JObject o = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                populations = (from n in o["nodes"] select (double)n[populationColumn]).ToArray();
                assignments = (from n in o["nodes"] select (int)n[assignmentColumn]).ToArray();

                foreach (string c in columnsToTrack)
                {
                    attributes[c] = (from n in o["nodes"] select (double)n[c]).ToArray();
                }
                if (regionAware)
                {
                    foreach ((string regionColumn, double regionDivisionPenalty) in regionDivisionSpecs)
                    {
                        var regionAssignments = (from n in o["nodes"] select (int)n[regionColumn]).ToArray();
                        regions[regionColumn] = (penalty: regionDivisionPenalty, mappings: regionAssignments);
                    }
                }

                EdgeTag getEdgeTag(int index, int u, int v)
                {
                    double divisionPenalty = 0;
                    foreach (KeyValuePair<string, (double penalty, int[] mappings)> region in regions)
                    {
                        if (region.Value.mappings[u] != region.Value.mappings[v])
                        {
                            divisionPenalty += region.Value.penalty;
                        }
                    }
                    return new EdgeTag(index, divisionPenalty);
                }
                int edgeIndex = 0;
                /// Nodes are assumed to be indexed from 0 to n-1 and listed in the json file in the order they are indexed.
                edges = o["adjacency"].SelectMany((x, i) => x.Select(e => {
                    int u = i;
                    int v = (int)e["id"];
                    return u < v ? new STaggedUndirectedEdge<int, EdgeTag>(u, v, getEdgeTag(edgeIndex++, u, v))
                                 : new STaggedUndirectedEdge<int, EdgeTag>(v, u, getEdgeTag(edgeIndex++, v, u));
                }));
            }

            bool oneIndexed = assignments.Min() == 1;

            Graph = new DualGraph
            {
                Populations = populations,
                TotalPop = populations.Sum(),
                Graph = edges.ToUndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>>(),
                Attributes = attributes.ToImmutableDictionary()
            };
            HasParent = false;
            /// Assignment column must be 0 or 1 indexed.
            Assignments = oneIndexed ? assignments.Select(d => d - 1).ToArray() : assignments;
            NumDistricts = oneIndexed ? assignments.Max() : assignments.Max() + 1;
            ScoreFunctions = Scores.ToDictionary(s => s.Name);
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentScoreValues = new Dictionary<string, ScoreValue>();
            CutEdges = Graph.Graph.Edges.Where(e => Assignments[e.Source] != Assignments[e.Target]);
        }

        /// <summary>
        /// Generate a child partition from a proposal.
        /// </summary>
        /// <param name="proposal">Proposal defining child partition. </param>
        public Partition(ReComProposal proposal)
        {
            Graph = proposal.Partition.Graph;
            ScoreFunctions = proposal.Partition.ScoreFunctions;
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentAssignments = proposal.Partition.Assignments;
            ParentScoreValues = proposal.Partition.ScoreValues;
            NumDistricts = proposal.Partition.NumDistricts;
            HasParent = true;
            Assignments = (int[])ParentAssignments.Clone();
            
            foreach ( var distAssignment in proposal.Flips)
            {
                int district = distAssignment.Key;
                foreach (int node in distAssignment.Value)
                {
                    Assignments[node] = district;
                }
            }
            CutEdges = Graph.Graph.Edges.Where(e => Assignments[e.Source] != Assignments[e.Target]);
            ProposalSummary = new ReComProposalSummary(proposal.DistrictsAffected, proposal.Flips, proposal.NewDistrictPops);
        }

        public Partition TakeSelfLoop()
        {
            SelfLoops++;
            return this;
        }

        /// <summary>
        /// Generate Subgraph view of the graph for the passed districts
        /// </summary>
        /// <param name="districts">The two districts to generate the subgraph of </param>
        /// <returns> New UndirectedGraph instance. </returns>
        public UndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>> DistrictSubGraph((int A, int B) districts)
        {
            Func<STaggedUndirectedEdge<int, EdgeTag>, bool> inDistricts = e => 
            {
                int sourceDist = Assignments[e.Source];
                int targetDist = Assignments[e.Target];
                bool sourceIn = sourceDist == districts.A || sourceDist == districts.B;
                bool targetIn = targetDist == districts.A || targetDist == districts.B;
                return sourceIn && targetIn;

            };
            // districts.Contains(Assignments[e.Source]) && districts.Contains(Assignments[e.Target])
            IEnumerable<STaggedUndirectedEdge<int, EdgeTag>> subgraphEdges = Graph.Graph.Edges.Where(e => inDistricts(e));
            return subgraphEdges.ToUndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>>();
        }
        
        /// <summary>
        /// Scores the partition on a metric
        /// </summary>
        /// <param name="Name">Name of the score to compute</param>
        /// <returns>ScoreValue for the partition</returns>
        /// <exception cref="ArgumentException">Thrown if the score name is unknown</exception>
        public ScoreValue Score(string Name)
        {
            if (ScoreValues.TryGetValue(Name, out ScoreValue value))
            {
                return value;
            }
            else if (ScoreFunctions.TryGetValue(Name, out Score score))
            {
                ScoreValue result = score.Func(this);
                ScoreValues[Name] = result;
                return result;
            }
            else 
            {
                throw new ArgumentException("Passed Score is not defined", Name);
            }
        }
        
        /// <summary>
        /// Gets the parent's ScoreValue associated with the passed metric name
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="parentScoreValue"></param>
        /// <returns> <c>true</c> if that score had already been computed for the parent partition and <c>false</c> otherwise. </returns>
        public bool TryGetParentScore(string Name, out ScoreValue parentScoreValue)
        {
            if (ParentScoreValues.TryGetValue(Name, out ScoreValue value))
            {
                parentScoreValue = value;
                return true;
            }
            parentScoreValue = default;
            return false;
        }
    }
}