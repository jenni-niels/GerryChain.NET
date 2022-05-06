using System;
using System.Collections.Generic;
using System.Linq;
using Google.OrTools.Graph;

namespace GerryChain
{
    public abstract record ScoreValue();
    public record PlanWideScoreValue(double Value)
        : ScoreValue();
    public record DistrictWideScoreValue(double[] Value)
        : ScoreValue();

    public record Score(string Name, Func<Partition, ScoreValue> Func);

    /// <summary>
    /// Factory Methods for common scores.
    /// </summary>
    public static class Scores
    {
        // Demographic Scores

        /// <summary>
        /// Factory method for a district level tally.
        /// </summary>
        /// <param name="name"> Name of the Tally </param>
        /// <param name="column"> Column in dualgraph to Tally </param>
        /// <returns> Score record defining a tally. </returns>
        public static Score TallyFactory(string name, string column)
        {
            Func<Partition, DistrictWideScoreValue> districtTally = partition =>
            {
                double[] districtSums;
                if (partition.TryGetParentScore(name, out ScoreValue parentScoreValue))
                {
                    ProposalSummary delta = partition.ProposalSummary;
                    districtSums = (double[])((DistrictWideScoreValue)parentScoreValue).Value.Clone();

                    districtSums[delta.DistrictsAffected.A] = delta.Flips[delta.DistrictsAffected.A].Select(n => partition.Graph.Attributes[column][n])
                                                                                                    .Sum();
                    districtSums[delta.DistrictsAffected.B] = delta.Flips[delta.DistrictsAffected.B].Select(n => partition.Graph.Attributes[column][n])
                                                                                                    .Sum();
                }
                else
                {
                    districtSums = new double[partition.NumDistricts];
                    Array.Clear(districtSums, 0, partition.NumDistricts);

                    for (int i = 0; i < partition.Assignments.Length; i++)
                    {
                        double nodeValue = partition.Graph.Attributes[column][i];
                        districtSums[partition.Assignments[i]] += nodeValue;
                    }
                }

                return new DistrictWideScoreValue(districtSums);
            };
            return new Score(name, districtTally);
        }
        public static List<Score> MinShareOverThresholdPlusNextHighest(string name, string minoirtyPopColumn, string popColumn, double threshold, string minPopTallyName = null, string popTallyName = null)
        {
            if (minPopTallyName is null)
            {
                minPopTallyName = minoirtyPopColumn;
            }
            if (popTallyName is null)
            {
                popTallyName = popColumn;
            }
            Func<Partition, PlanWideScoreValue> gingleatorFunc = partition =>
            {
                double[] minShares = ((DistrictWideScoreValue)partition.Score(minPopTallyName)).Value.Zip(((DistrictWideScoreValue)partition.Score(popTallyName)).Value, (b, v) => b / v).ToArray();
                double numOver = minShares.Where(perc => perc >= threshold).Count();
                var valsUnder = minShares.Where(perc => perc < threshold);
                double maxUnder = valsUnder.Count() == 0 ? 0 : valsUnder.Max();

                return new PlanWideScoreValue(numOver + maxUnder);
            };
            Score gingles = new Score(name, gingleatorFunc);
            var scores = new List<Score>();
            scores.Add(TallyFactory(minoirtyPopColumn, minPopTallyName));
            scores.Add(TallyFactory(popColumn, popTallyName));
            scores.Add(gingles);
            return scores;
        }

        // Compactness Scores
        
        public static Score NumCutEdgesFactory(string Name)
        {
            Func<Partition, PlanWideScoreValue> cutEdges = partition =>
            {
                return new PlanWideScoreValue(partition.CutEdges.Count());
            };
            return new Score(Name, cutEdges);
        }


        // Election Scores

        public static List<Score> ElectionTallyFactory(string[] electionCols)
        {
            var electScores = new List<Score>();
            foreach (string c in electionCols)
            {
                electScores.Add(TallyFactory(c, c));
            }
            return electScores;
        }

        public static double ElectionVoteShare((string Dem, string Rep) elect, Partition partition)
        {
            double demVotes = ((DistrictWideScoreValue) partition.Score(elect.Dem)).Value.Sum();
            double repVotes = ((DistrictWideScoreValue) partition.Score(elect.Rep)).Value.Sum();
            return demVotes / (demVotes + repVotes);
        }

        // Stable Proportionality: target proportionality on all elections.  (sum of absolute values) - normalize by number of election.
        public static Score StableProportionalityFactory(string name, (string Dem, string Rep, double VoteShare)[] elections)
        {
            int numElections = elections.Length;
            Func<Partition, PlanWideScoreValue> aggSeats = partition => {
                double totalDistance = 0;
                foreach ((string Dem, string Rep, double VoteShare) e in elections)
                {
                    double demSeats = 0;
                    double[] demVotes = ((DistrictWideScoreValue) partition.Score(e.Dem)).Value;
                    double[] repVotes = ((DistrictWideScoreValue) partition.Score(e.Rep)).Value;

                    for (int i = 0; i < partition.NumDistricts; i++)
                    {
                        if (demVotes[i] > repVotes[i]) { demSeats++; }
                    }
                    double demSeatShare = demSeats / partition.NumDistricts;
                    totalDistance += Math.Abs(demSeatShare - e.VoteShare);
                }
                return new PlanWideScoreValue(totalDistance / numElections);
            };
            
            return new Score(name, aggSeats);
        }

        // Responsive Proportionality: Average Proportionality over all elections vs. average vote share.
        public static Score AggregateDemSeatsFactory(string name, (string Dem, string Rep)[] elections)
        {
            Func<Partition, PlanWideScoreValue> aggSeats = partition => {
                double demSeats = 0;
                foreach ((string Dem, string Rep) e in elections)
                {
                    double[] demVotes = ((DistrictWideScoreValue) partition.Score(e.Dem)).Value;
                    double[] repVotes = ((DistrictWideScoreValue) partition.Score(e.Rep)).Value;

                    for (int i = 0; i < partition.NumDistricts; i++)
                    {
                        if (demVotes[i] > repVotes[i]) { demSeats++; }
                    }
                }
                return new PlanWideScoreValue(demSeats);
            };
            
            return new Score(name, aggSeats);
        }

        public static List<Score> ProportionalityDistanceScoresFactory(string stableProportionalityName, string responsiveProportionalityName, string aggregateSeatsName,
                                                                              (string Dem, string Rep)[] elections, Partition referencePartition)
        {
            var scores = new List<Score>();
            (string Dem, string Rep, double VoteShare)[] electionsWithVoteShares = elections.Select(e => (e.Dem, e.Rep, ElectionVoteShare(e, referencePartition))).ToArray();
            scores.Add(StableProportionalityFactory(stableProportionalityName, electionsWithVoteShares));
            scores.Add(AggregateDemSeatsFactory(aggregateSeatsName, elections));
            double proportionalityShare = electionsWithVoteShares.Select(e => e.VoteShare).Average();
            int totalSeats = referencePartition.NumDistricts * elections.Length;
            
            Func<Partition, PlanWideScoreValue> distance = partition => {
                double signedDistance = proportionalityShare - (((PlanWideScoreValue) partition.Score(aggregateSeatsName)).Value / totalSeats);
                return new PlanWideScoreValue(Math.Abs(signedDistance));
            };
            scores.Add(new Score(responsiveProportionalityName, distance));
            return scores;
        }

        // Misc Scores

        /// <summary>
        /// Computes the cost matrix of population overlap between all pairs of districts in the old
        /// and new plan.
        /// </summary>
        /// <param name="numOldDistricts"> The number of districts in the old plan. </param>
        /// <param name="oldAssignments"> The node assignments for the old plan. </param>
        /// <param name="newPlan"> The Partition object representing the old plan </param>
        /// <returns> populationOverlapMatrix: a long[,] with dimensions nxm where n is the number 
        /// of districts in the old plan and m is the number of districts in the new plan. 
        /// populationOverlapMatrix[i,j] is the number of people who lived in district i under the 
        /// old plan and will live in district j under th new plan. </returns>
        public static long[,] DistrictPopulationOverlapMatrix(int numOldDistricts, int[] oldAssignments,
                                                              Partition newPlan)
        {
            double[] nodePopulations = newPlan.Graph.Populations;
            double[,] populationOverlapMatrixRaw = new double[numOldDistricts, newPlan.NumDistricts];
            long[,] populationOverlapMatrix = new long[numOldDistricts, newPlan.NumDistricts];

            for (int i = 0; i < oldAssignments.Length; i++)
            {
                populationOverlapMatrixRaw[oldAssignments[i], newPlan.Assignments[i]] += nodePopulations[i];
            }

            for (int i = 0; i < numOldDistricts; i++)
            {
                for (int j = 0; j < newPlan.NumDistricts; j++)
                {
                    populationOverlapMatrix[i, j] = Convert.ToInt64(Math.Round(populationOverlapMatrixRaw[i, j]));
                }
            }
            return populationOverlapMatrix;
        }

        /// <summary>
        /// Factory method for the minimal population displacement between a plan and the passed
        /// reference plan.
        /// 
        /// The best district assignment between the 2 plans is computed by coverting this assignment
        /// problem to a minimum cost flow graph problem, via the
        /// <a href="https://en.wikipedia.org/wiki/Minimum-cost_flow_problem#Application">reduction
        /// between minimum weight bipartite matching and minimum cost flow</a>.  The optimal solution
        /// is then found using the MinCostFlow solver from Google OR-Tools.
        /// </summary>
        /// <param name="name"> The name of the score. </param>
        /// <param name="referencePlan"> Partition for which to compute population displacement
        /// against. </param>
        /// <returns> Score with Name `name` and Func to compute minimum population displacement. </returns>
        public static Score PopulationDisplacementFactory(string name, Partition referencePlan)
        {
            int numOldDistricts = referencePlan.NumDistricts;
            int[] oldAssignments = referencePlan.Assignments;

            Func<Partition, PlanWideScoreValue> displacement = partition =>
            {
                int numNewDistricts = partition.NumDistricts;
                int numMatches = Math.Min(numOldDistricts, numNewDistricts);
                long[,] populationOverlapMatrix = DistrictPopulationOverlapMatrix(numOldDistricts, oldAssignments, partition);

                int[] startNodes = Enumerable.Repeat(0, numOldDistricts)
                                             .Concat(Enumerable.Range(1, numOldDistricts).SelectMany(x => Enumerable.Repeat(x, numNewDistricts)))
                                             .Concat(Enumerable.Range(numOldDistricts + 1, numNewDistricts)).ToArray();

                int[] endNodes = Enumerable.Range(1, numOldDistricts)
                                           .Concat(Enumerable.Repeat(0, numOldDistricts).SelectMany(x => Enumerable.Range(numOldDistricts + 1, numNewDistricts)))
                                           .Concat(Enumerable.Repeat(numOldDistricts + numNewDistricts + 1, numNewDistricts)).ToArray();

                long[] costs = Enumerable.Repeat<long>(0, numOldDistricts)
                                         .Concat(Enumerable.Range(0, numOldDistricts).SelectMany(x => Enumerable.Range(0, numNewDistricts)
                                                                                                                .Select(y => populationOverlapMatrix[x, y])))
                                         .Concat(Enumerable.Repeat<long>(0, numNewDistricts)).ToArray();

                int[] supplies = Enumerable.Repeat(0, numOldDistricts + numNewDistricts).Prepend(numMatches).Append(-numMatches).ToArray();

                MinCostFlow minCostFlow = new MinCostFlow();
                // Add each arc.
                for (int i = 0; i < startNodes.Length; i++)
                {
                    minCostFlow.AddArcWithCapacityAndUnitCost(startNodes[i], endNodes[i], 1, -costs[i]);
                }
                // Add node supplies.
                for (int i = 0; i < supplies.Length; i++)
                {
                    minCostFlow.SetNodeSupply(i, supplies[i]);
                }
                MinCostFlow.Status status = minCostFlow.Solve();

                double populationOverlap = -1 * minCostFlow.OptimalCost();
                double minPopulationDisplaced = partition.Graph.TotalPop - populationOverlap;
                return new PlanWideScoreValue(minPopulationDisplaced);
            };
            return new Score(name,  displacement);
        }
    }
}


