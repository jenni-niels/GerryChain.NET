using System;
using System.Collections.Generic;
using System.Linq;
using GerryChain;
using Gurobi;

namespace GerryChainExtensions
{
    public static class ExtensionScores
    {
        public static Score PopulationDisplacement(string name, Partition enactedPlan)
        {
            int[] enactedAssignments = enactedPlan.Assignments;
            int numEnactedDistricts = enactedPlan.NumDistricts;
            double[] populations = enactedPlan.Graph.Populations;
            double totalPopulation = enactedPlan.Graph.TotalPop;

            Func<Partition, PlanWideScoreValue> displacement = partition =>
            {
                int numCurrentDistricts = partition.NumDistricts;
                GRBEnv env = new GRBEnv();
                GRBModel model = new GRBModel(env);
                var populationOverlap = new GRBLinExpr();

                GRBVar[,] vars = new GRBVar[numEnactedDistricts, numCurrentDistricts];

                for (int i = 0; i < numEnactedDistricts; i++)
                {
                    for (int j = 0; j < numCurrentDistricts; j++)
                    {
                        string varName = i.ToString() + "_" + j.ToString();
                        vars[i,j] = model.AddVar(0.0, 1.0, 0.0, GRB.BINARY, varName);
                    }
                }

                // collect expression for total people moving districts.
                for (int i = 0; i < numEnactedDistricts; i++)
                {
                    for (int j = 0; j < numCurrentDistricts; j++)
                    {
                        for (int n = 0; n < enactedAssignments.Length; n++)
                        {
                            if (enactedAssignments[n] == i && partition.Assignments[n] == j)
                            {
                                populationOverlap.AddTerm(populations[n], vars[i, j]);
                            }
                        }
                    }
                }

                // Each enacted district gets 1 or 0 current districts
                for (int i = 0; i < numEnactedDistricts; i++)
                {
                    var expr = new GRBLinExpr(0.0);
                    for (int j = 0; j < numCurrentDistricts; j++)
                    {
                        expr.AddTerm(1, vars[i, j]);
                    }
                    if (numEnactedDistricts > numCurrentDistricts)
                    {
                        model.AddConstr(expr <= 1.0, "equals 1");
                        model.AddConstr(expr >= 0.0, "equals 0");
                    }
                    else
                    {
                        model.AddConstr(expr == 1.0, "equals 1");
                    }
                }

                for (int j = 0; j < numCurrentDistricts; j++)
                {
                    var expr = new GRBLinExpr(0.0);
                    for (int i = 0; i < numEnactedDistricts; i++)
                    {
                        expr.AddTerm(1, vars[i, j]);
                    }
                    if (numCurrentDistricts > numEnactedDistricts)
                    {
                        model.AddConstr(expr <= 1.0, "equals 1");
                        model.AddConstr(expr >= 0.0, "equals 0");
                    }
                    else
                    {
                        model.AddConstr(expr == 1.0, "equals 1");
                    }
                }

                model.SetObjective(populationOverlap, GRB.MAXIMIZE);
                model.Optimize();

                // Console.WriteLine("Population Overlap: " + model.ObjVal);
                // Console.WriteLine("Population Displacement: " + (totalPopulation - model.ObjVal));
                // Print number mapping
                // for (int i = 0; i < numEnactedDistricts; i++)
                // {
                //     for (int j = 0; j < numCurrentDistricts; j++)
                //     {
                //         int val = (int)vars[i, j].X;
                //         if (val != 0)
                //         {
                //             Console.WriteLine(vars[i,j].VarName + ": " + vars[i,j].X);
                //         }
                //     }
                // }
                double minPopulationDisplaced = totalPopulation - model.ObjVal;
                model.Dispose();
                env.Dispose();
                return new PlanWideScoreValue(minPopulationDisplaced);
            };
            return new Score(name,  displacement);
        }
    }
}