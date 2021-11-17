using System;
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
                    /// TODO:: This computation can be optimized to be linear rather
                        // districtSums = Enumerable.Range(0, Partition.NumDistricts)
                        //                          .Select(d => Partition.Graph.Attributes[column].Where((v,i) => Partition.Assignments[i] == d).Sum())
                        //                          .ToArray();
                }

                return new DistrictWideScoreValue(districtSums);
            };
            return new Score(name, districtTally);
        }

        public static Score NumCutEdgesFactory()
        {
            Func<Partition, PlanWideScoreValue> cutEdges = Partition =>
            {
                return new PlanWideScoreValue(Partition.CutEdges.Count());
            };
            return new Score("NumCutEdges", cutEdges);
        }
    }
}


