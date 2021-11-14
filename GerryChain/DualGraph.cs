// using System;
// using System.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;
using QuikGraph;

namespace GerryChain
{
    /// <summary>
    /// Immutable representation of the dual graph on which partions are drawn.
    /// </summary>
    /// <remarks>
    /// Nodes are represented implicitly by indices.
    /// TODO:: This record/class does not contain many instance methods.  See other file.
    /// </remarks>
    public record DualGraph
    {
        public double TotalPop { get; init; }
        public UndirectedGraph<int, SUndirectedEdge<int>> Graph { get; init; }
        public double[] Populations { get; init; }
        public ImmutableDictionary<string, double[]> Attributes { get; init; }

        /// <summary>
        /// Generate Subgraph view of Graph instance from the passed nodes.
        /// </summary>
        /// <param name="nodes">The nodes that define the subgraph</param>
        /// <returns> New UndirectedGraph instance. </returns>
        public UndirectedGraph<int, SUndirectedEdge<int>> SubGraph(IEnumerable<int> nodes) {
            var subgraph = new UndirectedGraph<int, SUndirectedEdge<int>>();
            /// TODO:: Build Subgraph from graph.
            return subgraph;
        }
    }
}

