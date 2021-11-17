// using System;
// using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
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
}

