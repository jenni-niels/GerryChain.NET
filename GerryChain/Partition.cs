using System;
using System.Linq;

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
        public Partition(string jsonFilePath, string assignmentColumn, string[] columnsToTract)
        {
            HasParent = false;
            Graph = new DualGraph();
            // Assignments = assignment;
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