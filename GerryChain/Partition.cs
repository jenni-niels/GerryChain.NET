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
        public int[] ParentAssignments { get; private set; }


        /// <summary>
        /// Generate initial partition on dualgraph.
        /// </summary>
        /// <param name="graph"> Underlying Dual Graph </param>
        /// <param name="assignment"> Partition assignment on nodes of graph. </param>
        public Partition(DualGraph graph, int[] assignment)
        {
            HasParent = false;
            Graph = graph;
            Assignments = assignment;
        }

        /// <summary>
        /// Create a DualGraph representation for a state given a networkx json representation.
        /// </summary>
        /// <param name="jsonFilePath"> path to networkx json file </param>
        /// <param name="columnsToTract"> names of columns tracts as attributes </param>
        /// <returns> New instance of DualGraph record </returns>
        /// <remarks> Nodes are assumed to be indexed from 0 .. n-1 </remarks>
        public Partition(string jsonFilePath, string assignmentColumn, string populationColumn, string[] columnsToTract)
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

                foreach (string c in columnsToTract)
                {
                    attributes[c] = (from p in o["nodes"] select (double)p[c]).ToArray();
                }

                /// Nodes are assumed to be indexed from 0 to n-1 and listed in the json file in the order they are indexed.
                edges = o["adjacency"].SelectMany((x, i) => x.Select(e => new SUndirectedEdge<int>(i, (int)e["id"])));
            }

            Graph = new DualGraph
            {
                Populations = populations,
                TotalPop = populations.Sum(),
                Graph = edges.ToUndirectedGraph<int, SUndirectedEdge<int>>(),
                Attributes = attributes.ToImmutableDictionary()
            };
            HasParent = false;
            /// Assignment column must be 0 or 1 indexed.
            Assignments = (assignments.Min() == 1) ? assignments.Select(d => d - 1).ToArray() : assignments;
        }

        /// <summary>
        /// Generate 
        /// </summary>
        /// <param name="proposal"></param>
        public Partition(Proposal proposal)
        {
            Graph = proposal.Part.Graph;
            ParentAssignments = proposal.Part.Assignments;
            HasParent = true;
            Assignments = ParentAssignments.Select((district, i) => proposal.Flips.ContainsKey(i) ? proposal.Flips[i] : district).ToArray();
        }

    }

    /// TODO:: add Lazy evaluated scores on a partition
    public record PlanScore(string Name)
    {
        public string Name { get; } = Name;
        public Lazy<double> Value { get; }
    }
}