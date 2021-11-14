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
                /// TODO:: This computation can be optimized
                double[] districtSums = Enumerable.Range(0, Partition.NumDistricts)
                                                  .Select(d => Partition.Graph.Attributes[column].Where((v,i) => Partition.Assignments[i] == d).Sum())
                                                  .ToArray();

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