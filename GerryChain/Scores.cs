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

        public static Score NumCutEdgesFactory(string Name)
        {
            Func<Partition, PlanWideScoreValue> cutEdges = partition =>
            {
                return new PlanWideScoreValue(partition.CutEdges.Count());
            };
            return new Score(Name, cutEdges);
        }

        public static IEnumerable<Score> MinShareOverThresholdPlusNextHighest(string name, string minoirtyPopColumn, string popColumn, double threshold, string minPopTallyName=null, string popTallyName=null)
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
                double maxUnder = minShares.Where(perc => perc < threshold).Max();

                return new PlanWideScoreValue(numOver + maxUnder);
            };
            Score gingles = new Score(name, gingleatorFunc);
            var scores = new List<Score>();
            scores.Add(TallyFactory(minoirtyPopColumn, minPopTallyName));
            scores.Add(TallyFactory(popColumn, popTallyName));
            scores.Add(gingles);
            return scores ;
        }
    }
}


