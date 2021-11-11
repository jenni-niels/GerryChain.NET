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
        /// <param name="assignmentColumn"> Column name in the json file that contains the initial assignment </param>
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
                populations = (from p in o["nodes"] select (double)p[populationColumn]).ToArray();
                assignments = (from p in o["nodes"] select (int)p[assignmentColumn]).ToArray();

                foreach (string c in columnsToTrack)
                {
                    attributes[c] = (from p in o["nodes"] select (double)p[c]).ToArray();
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
        /// Generate 
        /// </summary>
        /// <param name="proposal"></param>
        public Partition(Proposal proposal)
        {
            Graph = proposal.Part.Graph;
            ScoreFunctions = proposal.Part.ScoreFunctions;
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentAssignments = proposal.Part.Assignments;
            HasParent = true;
            Assignments = ParentAssignments.Select((district, i) => proposal.Flips.ContainsKey(i) ? proposal.Flips[i] : district).ToArray();
        }
        
        public ScoreValue Score(string Name)
        {
            if (ScoreValues.ContainsKey(Name))
            {
                return ScoreValues[Name];
            }
            else if (ScoreFunctions.ContainsKey(Name))
            {
                ScoreValue result = ScoreFunctions[Name].Func(this);
                ScoreValues[Name] = result;
                return result;
            }
            else 
            {
                throw new ArgumentException("Passed Score is not defined", Name);
            }
        }

        public static Score Tally(string name, string column)
        {
            Func<Partition, DistrictWideScoreValue> districtTally = Partition =>
            {
                double[] districtSums = Enumerable.Range(0, Partition.NumDistricts).Select(d => Partition.Graph.Attributes[column].Where((v,i) => Partition.Assignments[i] == d).Sum()).ToArray();

                return new DistrictWideScoreValue(districtSums);
            };
            return new Score(name, districtTally);
        }
    }

    public abstract record ScoreValue();
    public record PlanWideScoreValue(double Value)
        : ScoreValue();
    public record DistrictWideScoreValue(double[] Value)
        : ScoreValue();

    public record Score(string Name, Func<Partition, ScoreValue> Func);
}