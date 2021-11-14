// using System;
using QuikGraph;
using System.Collections.Generic;
using System.Linq;
using GerryChain;


namespace GerryChainExtensions
{
    public static class ToyModels
    {
        /// <summary>
        /// Create Toy Grid Graph.  Used Primarily for testing.
        /// </summary>
        /// <param name="n">number of columns.</param>
        /// <param name="m">number of rows</param>
        /// <returns> New instance of DualGraph record </returns>
        /// TODO:: Add more options to the populations
        public static DualGraph GridGraph(int n, int m)
        {
            var edges = new List<IUndirectedEdge<int>>();
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
            var graph = edges.ToUndirectedGraph<int, IUndirectedEdge<int>>();
            var totalPop = n * m;
            var pops = Enumerable.Repeat<double>(1.0, n * m).ToArray();


            return new DualGraph
            {
                TotalPop = totalPop,
                Graph = graph,
                Populations = pops
            };
        }
    }
}