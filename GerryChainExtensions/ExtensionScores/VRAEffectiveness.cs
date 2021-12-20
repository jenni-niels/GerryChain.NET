using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using VRA;

namespace GerryChainExtensions
{
    public record VRAEffectivenessScoreValue(FSharpMap<string, double[]> Value)
        : ScoreValue;

    public static class VRAEffectivenessScores
    {
        public static IEnumerable<Score> GroupsVRAEffectivenessFactory(string name, string state, string alignmentType, string geoidCol, string[] groups = null)
        {
            var alignment = alignmentType switch
            {
                "CVAP" => AllignmentType.CVAP,
                "None" => AllignmentType.None,
                _ => throw new AggregateException($"Unsupported alignment type: {alignmentType}")
            };
            var VRA = new VRAAPI(state, alignment, geoidCol);

            Func<Partition, VRAEffectivenessScoreValue> vraEffectiveness = partition =>
            {
                FSharpMap<string, double[]> groupScores;
                if (partition.TryGetParentScore(name, out ScoreValue parentScoreValue))
                {
                    ProposalSummary delta = partition.ProposalSummary;
                    var districtsAffected = delta.DistrictsAffected;
                    var districts = new int[] { districtsAffected.A, districtsAffected.B };
                    groupScores = ((VRAEffectivenessScoreValue)parentScoreValue).Value;
                    var groupDeltaScores = VRA.InvokeDistrictDeltas(partition.Assignments, partition.Graph.Geoids, districts);

                    foreach (string group in groups)
                    {
                        groupScores[group][districtsAffected.A] = groupDeltaScores[group][districtsAffected.A.ToString()];
                        groupScores[group][districtsAffected.B] = groupDeltaScores[group][districtsAffected.B.ToString()];
                    }
                }
                else
                {
                    groupScores = VRA.Invoke(partition.Assignments, partition.Graph.Geoids);
                }
                return new VRAEffectivenessScoreValue(groupScores);
            };

            var vraEffectivenessScore = new Score(name, vraEffectiveness);
            var scores = new List<Score>();
            scores.Add(vraEffectivenessScore);

            if (groups is not null)
            {
                foreach (string group in groups)
                {
                    Func<Partition, DistrictWideScoreValue> groupEffectivness = partition =>
                    {
                        var scores = ((VRAEffectivenessScoreValue)partition.Score(name)).Value;
                        if (scores.TryGetValue(group, out double[] groupScores))
                        {
                            return new DistrictWideScoreValue(groupScores);
                        }
                        else
                        {
                            throw new ArgumentException($"Unsupported group: {group}");
                        }
                    };
                    var groupEffectivnessScore = new Score($"{name}_{group}", groupEffectivness);
                    scores.Add(groupEffectivnessScore);
                }
            }
            return scores;
        }

        public static Score VRAEffectivenessOverThresholdPlusNextHighest(string name, double threshold, string VRAEffectivenessScoreName, string[] groups)
        {

            Func<Partition, PlanWideScoreValue> gingleatorFunc = partition =>
            {
                var groupScores = groups.ToDictionary(group => group, group => ((DistrictWideScoreValue)partition.Score($"{VRAEffectivenessScoreName}_{group}")).Value);

                int numEffectiveDistricts = 0;
                for (int i = 0; i < partition.NumDistricts; i++)
                {
                    var effectiveGroups = groups.Where(group => groupScores[group][i] >= threshold).Count();
                    if (effectiveGroups > 0)
                    {
                        numEffectiveDistricts++;
                    }
                }
                double maxUnder = groups.Select(group => {
                    var valsUnder = groupScores[group].Where(e => e < threshold);
                    return valsUnder.Count() == 0 ? 0 : valsUnder.Max();
                }).Max();

                return new PlanWideScoreValue(numEffectiveDistricts + maxUnder);
            };
            return new Score(name, gingleatorFunc);
        }
    }
}