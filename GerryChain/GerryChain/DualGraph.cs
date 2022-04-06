using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuikGraph;

namespace GerryChain
{
    /// <summary>
    /// Immutable representation of the dual graph on which partions are drawn.
    /// </summary>
    /// <remarks>
    /// Nodes are represented implicitly by indices.
    /// </remarks>
    /// TODO:: Switch to Tagged Edges To represent which edges cross counties and the edge ids.
    public record DualGraph
    {
        public double TotalPop { get; init; }
        public UndirectedGraph<int, IUndirectedEdge<int>> Graph { get; init; }
        public Dictionary<long, double> RegionDivisionPenalties { get; init; }
        public double[] Populations { get; init; }
        public ImmutableDictionary<string, double[]> Attributes { get; init; }

        public string[] Geoids { get; init; }

        /// <summary>
        /// Helper function to hash edges by a long rather than the class type.
        /// </summary>
        /// <param name="e">edge to hash</param>
        /// <returns>ulong hash of edge</returns>
        /// TODO:: see if this can be replace by giving each edge and index.
        public static long EdgeHash(IUndirectedEdge<int> e)
        {
            return (long)e.Source << 32 ^ e.Target;
        }
    }

    public static class GraphParsers
    {
        public static (DualGraph graph, int[] assignments) LoadGraphFromJson(string jsonFilePath, string populationColumn, string[] columnsToTrack,
                          string assignmentColumn = null, bool regionAware = false, (string, double)[] regionDivisionSpecs = null,
                          string geoidCol = null)
        {
            if (regionAware && regionDivisionSpecs is null)
            {
                throw new ArgumentException("Cannot create region aware graph without region specification.");
            }

            double[] populations;
            int[] assignments = null;
            string[] geoids = null;
            var regions = new Dictionary<string, (double penalty, int[] mappings)>();
            IEnumerable<IUndirectedEdge<int>> edges;
            var attributes = new Dictionary<string, double[]>();

            using (StreamReader reader = File.OpenText(jsonFilePath))
            {
                JObject o = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                populations = (from n in o["nodes"] select (double)n[populationColumn]).ToArray();
                
                if (assignmentColumn is not null)
                {
                    assignments = (from n in o["nodes"] select (int)n[assignmentColumn]).ToArray();
                }

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
                if (geoidCol is not null)
                {
                    geoids = (from n in o["nodes"] select (string)n[geoidCol]).ToArray();
                }

                edges = o["adjacency"].SelectMany((x, i) => x.Select(e => (IUndirectedEdge<int>) new SUndirectedEdge<int>(i, (int)e["id"])));
            }
            // var regionDivisionPenalties = new Dictionary<long, double>();
            var regionDivisionPenalties = edges.ToDictionary(e => DualGraph.EdgeHash(e),
                                                             e => regions.Aggregate(0.0, (penalty, region) => penalty + (region.Value.mappings[e.Source] == region.Value.mappings[e.Target]
                                                                                                                        ? 0.0 : region.Value.penalty)));

            DualGraph g = new DualGraph
            {
                Populations = populations,
                TotalPop = populations.Sum(),
                Graph = edges.ToUndirectedGraph<int, IUndirectedEdge<int>>(),
                Attributes = attributes.ToImmutableDictionary(),
                RegionDivisionPenalties = regionDivisionPenalties,
                Geoids = geoids
            };
            
            return (g, assignments);
        }
    }
}

