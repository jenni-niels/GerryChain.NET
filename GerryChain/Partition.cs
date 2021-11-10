using System;
using System.Collections.Generic;
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
        public DualGraph graph { get; private set; }
        public int[] assignments { get; private set; }
        public bool hasParent { get; private set; }
        public int[] parentAssignments { get; private set; }


        public Partition(DualGraph graph, int[] assignment)
        {
            this.hasParent = false;
            this.graph = graph;
            this.assignments = assignment;
        }

        public Partition(Proposal proposal)
        {
            graph = proposal.part.graph;
            parentAssignments = proposal.part.assignments;
            hasParent = true;
            assignments = parentAssignments.Select((district, i) => proposal.flips.ContainsKey(i) ? proposal.flips[i] : district).ToArray();
        }

    }

    /// TODO:: add Lazy evaluated scores on a partition
    public record PlanScore(string Name)
    {
        public string Name { get; } = Name;
        public Lazy<double> Value { get; }
    }
    public record Proposal(Partition part, Dictionary<int, int> flips);
}