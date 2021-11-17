using System;
using System.Linq;

namespace GerryChain
{
     public static class AcceptanceFunctions
     {
         /// <summary>
         /// Returns an acceptance function to 
         /// </summary>
         /// <param name="targetScoreName">The name of a plan-wide score.</param>
         /// <param name="maximize"></param>
         /// <returns></returns>
         public static Func<Partition, int, double> SimulatedAnnealingFactory(Partition initialPartition, string targetScoreName, int durationHot, int durationCoolDown, int durationCold , bool maximize = true)
         {
            int cycleLength = durationHot + durationCoolDown + durationCold;
            double initialScore = ((PlanWideScoreValue)initialPartition.Score(targetScoreName)).Value;
            Func<Partition, int, double> simulatedAnnealingAccept = (partition, step) =>
            {
                int timeInCycle = step % cycleLength;
                double beta;
                double partScore = ((PlanWideScoreValue)partition.Score(targetScoreName)).Value;
                
                if (partition.TryGetParentScore(targetScoreName, out ScoreValue parentScoreValue) is false)
                {
                    throw new ArgumentException("Parent should have been scored.");
                }
                double scoreDelta = step == 1 ? partScore - initialScore : partScore - ((PlanWideScoreValue)parentScoreValue).Value;
                
                if (maximize is false)
                {
                    scoreDelta *= -1;
                }
                
                if (timeInCycle < durationHot)
                {
                    beta = 0.0;
                }
                else if (timeInCycle < durationHot + durationCoolDown)
                {
                    beta = (double) step / durationCoolDown; //todo
                }
                else
                {
                    beta = 1.0;
                }
                return Math.Exp(-beta * scoreDelta);
            };
            return simulatedAnnealingAccept;
        }

        public static Func<Partition, int, double> MetropolisHastingsFactory(Partition initialPartition, string targetScoreName, double beta, bool maximize=true)
        {
            double initialScore = ((PlanWideScoreValue)initialPartition.Score(targetScoreName)).Value;
            Func<Partition, int, double> metropolisHastingsAccept = (partition, step) =>
            {
                double partScore = ((PlanWideScoreValue)partition.Score(targetScoreName)).Value;
                
                if (partition.TryGetParentScore(targetScoreName, out ScoreValue parentScoreValue) is false)
                {
                    throw new ArgumentException("Parent should have been scored.");
                }
                double scoreDelta = step == 1 ? partScore - initialScore : partScore - ((PlanWideScoreValue)parentScoreValue).Value;
                
                if (maximize is false)
                {
                    scoreDelta *= -1;
                }
                return Math.Exp(-beta * scoreDelta);
            };
            return metropolisHastingsAccept;
        }
     }
}