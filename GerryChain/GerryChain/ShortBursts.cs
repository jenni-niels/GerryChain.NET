using System;
using System.Collections.Generic;

namespace GerryChain
{
    public interface ISingleMetricOptimizer
    {
        public string TargetScoreName { get; init; }
        public bool Maximize { get; init; }
        public Partition InitialPartition { get; init; }
        public Partition BestPartition { get; }
        public ScoreValue BestScore { get; }
        public Func<ScoreValue, ScoreValue, bool> BetterThanEqComparator { get; init; }
        public IEnumerable<Partition> Run(int randomSeed);
    }
    
    /// <summary>
    /// Implements the ISingleMetricOptimizer interface with the short bursts methodology.
    /// <br></br>
    /// Short bursts is an heuristic optimization method that chains together short explorers
    /// (bursts) by seeding the next burst with maximum observed value in the previous burst.
    /// (<see href="https://arxiv.org/abs/2011.02288">Read the paper here for more details.</see>)
    /// </summary>
    public class ShortBurstOptimizer : ISingleMetricOptimizer
    {
        public int BurstLength { get; init; }
        public int NumberOfBursts { get; init; }
        public string TargetScoreName { get; init; }
        public bool Maximize { get; init; }
        public Partition InitialPartition { get; init; }
        public Partition BestPartition { get; private set; }
        public ScoreValue BestScore { get; private set; }
        public Func<ScoreValue, ScoreValue, bool> BetterThanEqComparator { get; init; }

        // Markov Chain settings
        public int DegreeOfParallelism { get; init; }
        public int BatchSize { get; init; }
        public Func<Partition, int, double> AcceptanceFunction { get; init; }
        public double EpsilonBalance { get; init; }
        public double? PopulationTarget { get; init; }
        public HashSet<int> FrozenDistricts { get; init; }


        /// <summary>
        /// Creates instance of Short Burst Optimizer
        /// </summary>
        /// <param name="initialPartition"> Seed partition </param>
        /// <param name="burstLength"> How long should each burst be? </param>
        /// <param name="numberOfBursts"> How many bursts to chain together? </param>
        /// <param name="targetScoreName"> The name of the score that is being uses as the optimization
        /// metric. </param>
        /// <param name="epsilon"> Parameter setting how tightly to balance population. Districts are
        /// required to have population between (1 - \epsilon) * ideal population and 
        /// (1 + \epsilon) * ideal population. </param>
        /// <param name="accept"> The acceptance function to use in the Markov Chain. </param>
        /// <param name="degreeOfParallelism"> Maximum number of proposals generation tasks to execute
        /// in parallel. If 0, the default system behavior is used. </param>
        /// <param name="batchSize"> How many proposals to try to generate at each step. </param>
        /// <param name="frozenDistricts"> Set of district ids to "freeze" and not allow to change
        /// in the course of the chain. </param>
        /// <param name="isBetterEqThan"> Lambda function that defines comparisions between
        /// ScoreValues.  The first argument is ScoreValue of the current partition. The second argument 
        /// is current ScoreValue for the current best partition. Returns true if current partition is 
        /// **better or the same** as the current best score and false otherwise.  If null, ScoreValues
        /// are assumed to be PlanWideScoreValues and uses >= or <= based on value of `maximize`. </param>
        /// <param name="maximize"> Only used if `isBetterEqThan` is null.  Specifies whether to maximize
        /// or minimize. </param>
        
        public ShortBurstOptimizer(Partition initialPartition, int burstLength, int numberOfBursts, string targetScoreName,
                                   double epsilon, Func<Partition, int, double> accept = null, int degreeOfParallelism = 0, 
                                   int batchSize = 32, HashSet<int> frozenDistricts = null, double? populationTarget = null,
                                   bool maximize = true, Func<ScoreValue, ScoreValue, bool> isBetterEqThan = null)
        {
            BurstLength = burstLength;
            NumberOfBursts = numberOfBursts;
            InitialPartition = initialPartition;
            TargetScoreName = targetScoreName;
            Maximize = maximize;
            BetterThanEqComparator = (isBetterEqThan is null) ? (scoreVal, _) => IsImprovementPlanWideScore((PlanWideScoreValue) scoreVal)
                                                               : isBetterEqThan;

            EpsilonBalance = epsilon;
            PopulationTarget = populationTarget;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="randomSeed"> Value to seed the random number generator with. </param>
        /// <returns></returns>
        public IEnumerable<Partition> Run(int randomSeed = 0)
        {
            BestPartition = InitialPartition;
            BestScore = BestPartition.Score(TargetScoreName);
            for (int i = 0; i < NumberOfBursts; i++)
            {
                int burstSeed = randomSeed + i;
                Chain burstChain = new Chain(BestPartition, BurstLength, EpsilonBalance, randomSeed: burstSeed,
                                             accept: AcceptanceFunction, degreeOfParallelism: DegreeOfParallelism,
                                             batchSize: BatchSize, frozenDistricts: FrozenDistricts, 
                                             populationTarget: PopulationTarget);
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