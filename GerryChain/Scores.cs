using System;
using System.Collections.Generic;
using System.Linq;

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
    }
}


