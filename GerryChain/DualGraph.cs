// using System;
using System.Linq;
using System.Collections.Immutable;
using QuikGraph;
using System.Json;

namespace GerryChain
{
    /// <summary>
    /// Immutable representation of the dual graph on which partions are drawn.
    /// </summary>
    /// <remarks>
    /// Nodes are represented implicitly by indices.
    /// </remarks>
    public record DualGraph
    {

        public double TotalPop { get; init; }
        // public Edge[] Edges { get; init; }
        public UndirectedGraph<int, SUndirectedEdge<int>> graph {get; init;}
        public double[] Populations {get; init;}
        public ImmutableDictionary<string, double[]> Attributes {get; init;}
        
        public DualGraph FromJson(string jsonFilePath){
            return new DualGraph();
        }

        public DualGraph GridGraph(uint n, uint m, double[] Populations){
            var totalPop = Populations.Sum();


            return new DualGraph { TotalPop = totalPop,
                                   Populations = Populations };
        }

    };

    /// <summary>
    /// Represents an edge of the graph between two units.
    /// </summary>
    /// <param name="u">
    /// The id of the first node
    /// </param>
    /// <param name="v">
    /// The id of the second node
    /// </param>
    /// <remarks>
    /// u <= v
    /// </remarks>
    public record Edge(uint u, uint v);
}

