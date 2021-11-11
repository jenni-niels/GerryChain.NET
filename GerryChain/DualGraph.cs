﻿// using System;
using System.Linq;
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

        /// <summary>
        /// Create Toy Grid Graph.  Used Primarily for testing.
        /// </summary>
        /// <param name="n">number of columns.</param>
        /// <param name="m">number of rows</param>
        /// <returns> New instance of DualGraph record </returns>
        public static DualGraph GridGraph(int n, int m)
        {
            var edges = new List<SUndirectedEdge<int>>();
            for (int col = 0; col < n; col++)
            {
                for (int row = col; row < m; row++)
                {
                    int i = (col * m) + row;
                    if (col > 0)
                    {
                        int westNeighbor = ((col - 1) * m) + row;
                        edges.Add(new SUndirectedEdge<int>(i, westNeighbor));
                    }
                    if (col < n - 1)
                    {
                        int eastNeighbor = ((col + 1) * m) + row;
                        edges.Add(new SUndirectedEdge<int>(i, eastNeighbor));
                    }
                    if (row > 0)
                    {
                        int southNeighbor = (col * m) + (row - 1);
                        edges.Add(new SUndirectedEdge<int>(i, southNeighbor));
                    }
                    if (row < m - 1)
                    {
                        int northNeighbor = (col * m) + (row + 1);
                        edges.Add(new SUndirectedEdge<int>(i, northNeighbor));
                    }
                }
            }
            var graph = edges.ToUndirectedGraph<int, SUndirectedEdge<int>>();
            var totalPop = n * m;
            var pops = Enumerable.Repeat<double>(1.0, n * m).ToArray();


            return new DualGraph
            {
                TotalPop = totalPop,
                Graph = graph,
                Populations = pops
            };
        }
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

