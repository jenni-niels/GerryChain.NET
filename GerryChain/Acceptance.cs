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
        /// <param name="minimize"></param>
        /// <returns></returns>
        public static Func<Partition, int, double> SimulatedAnnealingFactory(Partition initialPartition, string targetScoreName, int durationHot, int durationCoolDown, int durationCold, double betaMagnitude, bool minimize = true)
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
                
                if (minimize is false)
                {
                    scoreDelta *= -1;
                }
                
                if (timeInCycle < durationHot)
                {
                    beta = 0.0*betaMagnitude;
                }
                else if (timeInCycle < durationHot + durationCoolDown)
                {
                    beta = (double) (timeInCycle - durationHot) / durationCoolDown*betaMagnitude; //todo
                }
                else
                {
                    beta = 1.0*betaMagnitude;
                }
                return Math.Exp(-beta * scoreDelta);
            };
            return simulatedAnnealingAccept;
        }

        public static Func<Partition, int, double> SimulatedAnnealingFactory(Partition initialPartition, string targetScoreName, Func<int, double> betaFunction, double betaMagnitude, bool minimize = true)
        {
            double initialScore = ((PlanWideScoreValue)initialPartition.Score(targetScoreName)).Value;
            Func<Partition, int, double> simulatedAnnealingAccept = (partition, step) =>
            {
                double beta = betaFunction(step);
                double partScore = ((PlanWideScoreValue)partition.Score(targetScoreName)).Value;
                
                if (partition.TryGetParentScore(targetScoreName, out ScoreValue parentScoreValue) is false)
                {
                    throw new ArgumentException("Parent should have been scored.");
                }
                double scoreDelta = step == 1 ? partScore - initialScore : partScore - ((PlanWideScoreValue)parentScoreValue).Value;
                
                if (minimize is false) { scoreDelta *= -1;}
                
                return Math.Exp(-beta * betaMagnitude * scoreDelta);
            };
            return simulatedAnnealingAccept;
        }

        public static Func<Partition, int, double> MetropolisHastingsFactory(Partition initialPartition, string targetScoreName, double beta, bool minimize=true)
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
                
                if (minimize is false)
                {
                    scoreDelta *= -1;
                }
                return Math.Exp(-beta * scoreDelta);
            };
            return metropolisHastingsAccept;
        }
     }
}