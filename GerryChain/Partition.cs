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
        private Dictionary<string, Score> ScoreFunctions { get; set; }
        private Dictionary<string, ScoreValue> ScoreValues { get; set; }

        /// <summary>
        /// Generate initial partition on dualgraph.
        /// </summary>
        /// <param name="graph"> Underlying Dual Graph </param>
        /// <param name="assignment"> Partition assignment on nodes of graph. </param>
        public Partition(DualGraph graph, int[] assignment, IEnumerable<Score> Scores)
        {
            HasParent = false;
            Graph = graph;
            Assignments = assignment;
            ScoreFunctions = Scores.ToDictionary(s => s.Name);
            ScoreValues = new Dictionary<string, ScoreValue>();
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
        public Partition(string jsonFilePath, string assignmentColumn, string populationColumn, string[] columnsToTrack, IEnumerable<Score> Scores)
        {

            double[] populations;
            int[] assignments;
            IEnumerable<SUndirectedEdge<int>> edges;
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

                /// Nodes are assumed to be indexed from 0 to n-1 and listed in the json file in the order they are indexed.
                edges = o["adjacency"].SelectMany((x, i) => x.Select(e => new SUndirectedEdge<int>(i, (int)e["id"])));
            }

            bool oneIndexed = (assignments.Min() == 1);

            Graph = new DualGraph
            {
                Populations = populations,
                TotalPop = populations.Sum(),
                Graph = edges.ToUndirectedGraph<int, SUndirectedEdge<int>>(),
                Attributes = attributes.ToImmutableDictionary()
            };
            HasParent = false;
            /// Assignment column must be 0 or 1 indexed.
            Assignments = oneIndexed ? assignments.Select(d => d - 1).ToArray() : assignments;
            NumDistricts = oneIndexed ? assignments.Max() : assignments.Max() + 1;
            ScoreFunctions = Scores.ToDictionary(s => s.Name);
            ScoreValues = new Dictionary<string, ScoreValue>();
        }

        /// <summary>
        /// Generate a child partition from a proposal.
        /// </summary>
        /// <param name="proposal">Proposal defining child partition. </param>
        public Partition(Proposal proposal)
        {
            Graph = proposal.Partition.Graph;
            ScoreFunctions = proposal.Partition.ScoreFunctions;
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentAssignments = proposal.Partition.Assignments;
            HasParent = true;
            Assignments = ParentAssignments.Select((district, i) => proposal.Flips.ContainsKey(i) ? proposal.Flips[i] : district).ToArray();
        }

        /// <summary>
        /// Generate Subgraph view of the graph for the passed districts
        /// </summary>
        /// <param name="districts">The two districts to generate the subgraph of </param>
        /// <returns> New UndirectedGraph instance. </returns>
        public UndirectedGraph<int, SUndirectedEdge<int>> DistrictSubGraph(HashSet<int> districts) {
            IEnumerable<SUndirectedEdge<int>> subgraphEdges = Graph.Graph.Edges.Where(e => districts.Contains(Assignments[e.Source]) && districts.Contains(Assignments[e.Target]));
            return subgraphEdges.ToUndirectedGraph<int, SUndirectedEdge<int>>();
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
    }
}