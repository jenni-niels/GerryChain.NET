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
            Func<Partition, DistrictWideScoreValue> districtTally = Partition =>
            {
                double[] districtSums;
                if (Partition.TryGetParentScore(name, out ScoreValue parentScoreValue))
                {
                    ReComProposalSummary delta = Partition.ProposalSummary;
                    districtSums = (double[])((DistrictWideScoreValue)parentScoreValue).Value.Clone();

                    districtSums[delta.DistrictsAffected.A] = delta.Flips[delta.DistrictsAffected.A].Select(n => Partition.Graph.Attributes[column][n])
                                                                                                    .Sum();
                    districtSums[delta.DistrictsAffected.B] = delta.Flips[delta.DistrictsAffected.B].Select(n => Partition.Graph.Attributes[column][n])
                                                                                                    .Sum();
                }
                else
                {
                    districtSums = new double[Partition.NumDistricts];
                    Array.Clear(districtSums, 0, Partition.NumDistricts);

                    for (int i = 0; i < Partition.Assignments.Length; i++)
                    {
                        double nodeValue = Partition.Graph.Attributes[column][i];
                        districtSums[Partition.Assignments[i]] += nodeValue;
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