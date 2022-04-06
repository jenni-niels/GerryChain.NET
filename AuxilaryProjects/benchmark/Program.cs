using System;
using System.Diagnostics;

using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using CommandLine;
using CommandLine.Text;

using GerryChain;

namespace benchmark
{
    public class Options {

        [Option(shortName: 'n', longName: "steps", HelpText = "Number of Steps to Run on the Chain", Default=10)]
        public int steps { get; set; }


        [Option(longName: "batchSize", HelpText = "Proposals to run in parallel ", Default=2)]
        public int batchSize { get; set; }
        
        [Option(longName: "degreeOfParallelism", HelpText = "Cores to use in parallel", Default=0)]
        public int degreeOfParallelism { get; set; }
    }
    class Program {

        public static void Runner(int stepCount, int batchSize, int degreeOfParallelism) {
            Console.WriteLine($"Starting A Chain...");
            Console.WriteLine($"stepCount: {stepCount}");
            Console.WriteLine($"batchSize: {batchSize}");
            Console.WriteLine($"degreeOfParallelism: {degreeOfParallelism}");
            Console.WriteLine($"");

            Stopwatch sw = Stopwatch.StartNew();

            var TallyBVAP = Scores.TallyFactory("BVAP", "BVAP");
            var TallyVAP = Scores.TallyFactory("VAP", "VAP");
            var TallyPOP = Scores.TallyFactory("TOTPOP", "TOTPOP");
            var initPart = new Partition("../resources/al_vtds20_with_seeds.json", "CD_Seed", "TOTPOP", new string[] { "TOTPOP", "VAP", "BVAP" }, new Score[] { TallyBVAP, TallyPOP, TallyVAP });
            var chain = new Chain(initPart, stepCount, 0.01, batchSize:batchSize, degreeOfParallelism:degreeOfParallelism);
            Console.WriteLine($"Loading data took: {sw.Elapsed.TotalSeconds:F6} seconds");
            sw = Stopwatch.StartNew();
            var bs = chain.Select(p => ((DistrictWideScoreValue) p.Score("BVAP")).Value.Zip(((DistrictWideScoreValue)p.Score("VAP")).Value, (b, v) => b/v)).ToArray();
            Console.WriteLine($"Chain took: {sw.Elapsed.TotalSeconds:F6} seconds");

        }
        public static void Main(string[] args) {
            Parser.Default.ParseArguments<Options>(args)
                        .WithParsed<Options> ( o =>
                        {
                            Runner(o.steps, o.batchSize, o.degreeOfParallelism);
                        });
        }
    }
}