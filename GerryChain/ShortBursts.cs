using System;
using System.Collections.Generic;

namespace GerryChain
{
    public class ShortBurstOptimizer //: IEnumerable<Partition>
    {
        public int BurstLength { get; init; }
        public int NumberOfBursts { get; init; }
        public string TargetScoreName { get; init; }
        public bool Maximize { get; init; }
        public Partition BestPartition { get; private set; }
        public ScoreValue BestScore { get; private set; }
        private Func<ScoreValue, ScoreValue, bool> BetterThanEqComparator { get; init; }

        // Markov Chain settings
        public int RngSeed { get; init; }
        public int DegreeOfParallelism { get; init; }
        public int BatchSize { get; init; }
        public Func<Partition, int, double> AcceptanceFunction { get; init; }
        public double EpsilonBalance { get; init; }
        public HashSet<int> FrozenDistricts { get; init; }


        /// <summary>
        /// Creates instance of Short Burst Optimizer
        /// </summary>
        /// <param name="initialPartition"></param>
        /// <param name="burstLength"></param>
        /// <param name="numberOfBursts"></param>
        /// <param name="targetScoreName"></param>
        /// <param name="epsilon"></param>
        /// <param name="randomSeed"></param>
        /// <param name="accept"></param>
        /// <param name="degreeOfParallelism"></param>
        /// <param name="batchSize"></param>
        /// <param name="frozenDistricts"></param>
        /// <param name="isBetterEqThan"> Lambda function that defines comparisions between
        /// ScoreValues.  The first argument is ScoreValue of the current partition. The second argument 
        /// is current ScoreValue for the current best partition. Returns true if current partition is 
        /// **better or the same** as the current best score and false otherwise.  If null, ScoreValues
        /// are assumed to be PlanWideScoreValues and uses >= or <= based on value of `maximize`.</param>
        /// <param name="maximize">Only used if `isBetterEqThan` is null.  Specifies whether to maximize
        /// or minimize.</param>
        
        public ShortBurstOptimizer(Partition initialPartition, int burstLength, int numberOfBursts, string targetScoreName,
                                   double epsilon, int randomSeed = 0, Func<Partition, int, double> accept = null, 
                                   int degreeOfParallelism = 0, int batchSize = 32, HashSet<int> frozenDistricts = null,
                                   Func<ScoreValue, ScoreValue, bool> isBetterEqThan = null, bool maximize = true)
        {
            BurstLength = burstLength;
            NumberOfBursts = numberOfBursts;
            BestPartition = initialPartition;
            TargetScoreName = targetScoreName;
            BestScore = BestPartition.Score(TargetScoreName);
            Maximize = maximize;
            BetterThanEqComparator = (isBetterEqThan is null) ? (scoreVal, _) => IsImprovementPlanWideScore((PlanWideScoreValue) scoreVal)
                                                               : isBetterEqThan;

            EpsilonBalance = epsilon;
            RngSeed = randomSeed;
            BatchSize = batchSize;
            AcceptanceFunction = accept;
            DegreeOfParallelism = degreeOfParallelism;
            FrozenDistricts = frozenDistricts;
        }

        private bool IsImprovementPlanWideScore(PlanWideScoreValue partScore)
        {
            if (Maximize) { return partScore.Value >= ((PlanWideScoreValue)BestScore).Value; }
            else { return partScore.Value <= ((PlanWideScoreValue)BestScore).Value; }
        }
        public IEnumerable<Partition> Run()
        {
            for (int i = 0; i < NumberOfBursts; i++)
            {
                int adjustedSeed = RngSeed + i;
                Chain burstChain = new Chain(BestPartition, BurstLength, EpsilonBalance, randomSeed: adjustedSeed,
                                             accept: AcceptanceFunction, degreeOfParallelism: DegreeOfParallelism,
                                             batchSize: BatchSize, frozenDistricts: FrozenDistricts);
                foreach (Partition part in burstChain)
                {
                    yield return part;
                    ScoreValue curScore = part.Score(TargetScoreName);
                    if (BetterThanEqComparator(curScore, BestScore))
                    {
                        BestPartition = part;
                        BestScore = curScore;
                    }
                }
            }
        }
    }
}