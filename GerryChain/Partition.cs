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
        public int NumDistricts { get; private set; }
        public int[] ParentAssignments { get; private set; }
        public ProposalSummary ProposalSummary { get; private set; }
        public int SelfLoops { get; private set; } = 0;

        public IEnumerable<IUndirectedEdge<int>> CutEdges { get; private set; }

        private Dictionary<string, Score> ScoreFunctions { get; set; }
        private Dictionary<string, ScoreValue> ScoreValues { get; set; }
        private Dictionary<string, ScoreValue> ParentScoreValues { get; set; }

        /// <summary>
        /// Generate initial partition on dualgraph.
        /// </summary>
        /// <param name="graph"> Underlying Dual Graph </param>
        /// <param name="assignment"> Partition assignment on nodes of graph. </param>
        public Partition(DualGraph graph, int[] assignment, IEnumerable<Score> scores = null, Partition parent = null)
        {
            Graph = graph;
            ScoreValues = new Dictionary<string, ScoreValue>();
            CutEdges = Graph.Graph.Edges.Where(e => Assignments[e.Source] != Assignments[e.Target]);
            bool oneIndexed = assignment.Min() == 1;
            Assignments = oneIndexed ? assignment.Select(d => d - 1).ToArray() : assignment;
            HasParent = parent is not null;
            ParentScoreValues = new Dictionary<string, ScoreValue>(); //move this

            if (parent is null)
            {
                ScoreFunctions = scores.ToDictionary(s => s.Name);
                NumDistricts = oneIndexed ? assignment.Max() : assignment.Max() + 1;
            }
            else
            {
                ParentAssignments = parent.Assignments;
                // ParentScoreValues = parent.ScoreValues;
                ScoreFunctions = parent.ScoreFunctions;
                NumDistricts = parent.NumDistricts;
            }
        }

        /// <summary>
        /// Create a DualGraph representation for a state given a networkx json representation.
        /// </summary>
        /// <param name="jsonFilePath"> path to networkx json file </param>
        /// <param name="assignmentColumn"> Column name in the json file that contains the initial
        /// assignment.  The values of the assignment column must be 0 or 1 indexed.</param>
        /// <param name="populationColumn"> Column name in the json file that contains the population of each node. </param>
        /// <param name="columnsToTrack"> names of columns tracts as attributes </param>
        /// <returns> New instance of DualGraph record </returns>
        /// <remarks> Nodes are assumed to be indexed from 0 .. n-1 </remarks>
        public Partition(string jsonFilePath, string assignmentColumn, string populationColumn, string[] columnsToTrack,
                         IEnumerable<Score> scores, bool regionAware = false, (string, double)[] regionDivisionSpecs = null,
                         bool reorderNumbers = false, string geoidCol = null)
        {
            var g = GraphParsers.LoadGraphFromJson(jsonFilePath, populationColumn, columnsToTrack, 
                                                    assignmentColumn, regionAware, regionDivisionSpecs,
                                                    geoidCol);
            Graph = g.graph;
            int[] assignments = g.assignments;
            HasParent = false;
            /// Assignment column must be 0 or 1 indexed.
            if (reorderNumbers)
            {
                int newDistrict = 0;
                var districtKeys = assignments.Distinct().ToDictionary(d => d, d => newDistrict++);
                NumDistricts = districtKeys.Count;
                Assignments = assignments.Select(d => districtKeys[d]).ToArray();
            }
            else
            {
                bool oneIndexed = assignments.Min() == 1;
                Assignments = oneIndexed ? assignments.Select(d => d - 1).ToArray() : assignments;
                NumDistricts = oneIndexed ? assignments.Max() : assignments.Max() + 1;
            }
            ScoreFunctions = scores.ToDictionary(s => s.Name);
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentScoreValues = new Dictionary<string, ScoreValue>();
            CutEdges = Graph.Graph.Edges.Where(e => Assignments[e.Source] != Assignments[e.Target]);
        }

        /// <summary>
        /// Generate a child partition from a proposal.
        /// </summary>
        /// <param name="proposal">Proposal defining child partition. </param>
        public Partition(Proposal proposal)
        {
            Graph = proposal.Partition.Graph;
            ScoreFunctions = proposal.Partition.ScoreFunctions;
            ScoreValues = new Dictionary<string, ScoreValue>();
            ParentAssignments = proposal.Partition.Assignments;
            ParentScoreValues = proposal.Partition.ScoreValues;
            NumDistricts = proposal.Partition.NumDistricts;
            HasParent = true;
            Assignments = (int[])ParentAssignments.Clone();
            
            foreach ( var distAssignment in proposal.Flips)
            {
                int district = distAssignment.Key;
                foreach (int node in distAssignment.Value)
                {
                    Assignments[node] = district;
                }
            }
            CutEdges = Graph.Graph.Edges.Where(e => Assignments[e.Source] != Assignments[e.Target]);
            ProposalSummary = new ProposalSummary(proposal.DistrictsAffected, proposal.Flips, proposal.NewDistrictPops);
        }

        public Partition TakeSelfLoop()
        {
            SelfLoops++;
            return this;
        }

        /// <summary>
        /// Generate Subgraph view of the graph for the passed districts
        /// </summary>
        /// <param name="districts">The two districts to generate the subgraph of </param>
        /// <returns> New UndirectedGraph instance. </returns>
        public UndirectedGraph<int, IUndirectedEdge<int>> DistrictSubGraph((int A, int B) districts)
        {
            Func<IUndirectedEdge<int>, bool> inDistricts = e => 
            {
                int sourceDist = Assignments[e.Source];
                int targetDist = Assignments[e.Target];
                bool sourceIn = sourceDist == districts.A || sourceDist == districts.B;
                bool targetIn = targetDist == districts.A || targetDist == districts.B;
                return sourceIn && targetIn;
            };
            // districts.Contains(Assignments[e.Source]) && districts.Contains(Assignments[e.Target])
            IEnumerable<IUndirectedEdge<int>> subgraphEdges = Graph.Graph.Edges.Where(e => inDistricts(e));
            return subgraphEdges.ToUndirectedGraph<int, IUndirectedEdge<int>>();
        }
        
        /// <summary>
        /// Gets the ScoreValue associated with the passed metric name.
        /// </summary>
        /// <param name="Name">Name of the score to compute</param>
        /// <returns>ScoreValue for the partition</returns>
        /// <exception cref="ArgumentException">Thrown if the score name is unknown</exception>
        public ScoreValue Score(string Name)
        {
            if (ScoreValues.TryGetValue(Name, out ScoreValue value))
            {
                return value;
            }
            else if (ScoreFunctions.TryGetValue(Name, out Score score))
            {
                ScoreValue result = score.Func(this);
                ScoreValues[Name] = result;
                return result;
            }
            else 
            {
                throw new ArgumentException("Passed Score is not defined", Name);
            }
        }

        /// <summary>
        /// Gets the parent's ScoreValue associated with the passed metric name
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="parentScoreValue"></param>
        /// <returns> <c>true</c> if that score had already been computed for the parent partition and <c>false</c> otherwise. </returns>
        public bool TryGetParentScore(string Name, out ScoreValue parentScoreValue)
        {
            if (ParentScoreValues.TryGetValue(Name, out ScoreValue value))
            {
                parentScoreValue = value;
                return true;
            }
            parentScoreValue = default;
            return false;
        }
    }
}