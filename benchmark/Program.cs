using System;
using System.Diagnostics;

using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GerryChain;

namespace benchmark
{
    class Program {
        public static void Main() {
            Stopwatch sw = Stopwatch.StartNew();

            var TallyBVAP = Scores.TallyFactory("BVAP", "BVAP");
            var TallyVAP = Scores.TallyFactory("VAP", "VAP");
            var TallyPOP = Scores.TallyFactory("TOTPOP", "TOTPOP");
            var initPart = new Partition("../resources/al_vtds20_with_seeds.json", "CD_Seed", "TOTPOP", new string[] { "TOTPOP", "VAP", "BVAP" }, new Score[] { TallyBVAP, TallyPOP, TallyVAP });
            var chain = new ReComChain(initPart, 10, 0.01, batchSize:256);
            Console.WriteLine($"Loading data took: {sw.Elapsed.Seconds:F6} seconds");
            sw = Stopwatch.StartNew();
            var bs = chain.Select(p => ((DistrictWideScoreValue) p.Score("BVAP")).Value.Zip(((DistrictWideScoreValue)p.Score("VAP")).Value, (b, v) => b/v)).ToArray();
            Console.WriteLine($"Chain took: {sw.Elapsed.Seconds:F6} seconds");

        }
    }
}