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
    /// TODO:: Switch to Tagged Edges To represent which edges cross counties and the edge ids.
    public record DualGraph
    {
        public double TotalPop { get; init; }
        public UndirectedGraph<int, STaggedUndirectedEdge<int, EdgeTag>> Graph { get; init; }
        public double[] Populations { get; init; }
        public ImmutableDictionary<string, double[]> Attributes { get; init; }
    }

    public record EdgeTag(int ID, double RegionDivisionPenalty);
}

