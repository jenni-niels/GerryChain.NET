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
    /// </remarks>
    public record DualGraph
    {
        public double TotalPop { get; init; }
        public UndirectedGraph<int, SUndirectedEdge<int>> Graph { get; init; }
        public double[] Populations { get; init; }
        public ImmutableDictionary<string, double[]> Attributes { get; init; }
    }
}

