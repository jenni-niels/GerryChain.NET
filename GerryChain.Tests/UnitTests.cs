using System;
using Xunit;
using System.Linq;
using GerryChainExtensions;

namespace GerryChain.Tests
{
    public record Paths() {
        public string executingAssemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }
    public class UnitTests
    {
        /// <summary>
        /// Population of grid graph is rows * columns.
        /// </summary>
        [Fact]
        public void DualGraphTest1()
        {
            DualGraph d = ToyModels.GridGraph(5,5);
            Assert.Equal((double) (5*5), d.TotalPop);
        }
        /// <summary>
        /// Initial Partition has no parent
        /// </summary>
        [Fact]
        public void PartitionTest1()
        {
            var path = new Paths();
            var filePath = System.IO.Path.Combine(path.executingAssemblyDir, "al_vtds20_with_seeds.json");
            Partition p = new Partition(filePath, "CD_Seed", "TOTPOP", new string[] { "VAP", "BVAP" }, new Score[] { });
            Assert.False(p.HasParent);
        }

        /// <summary>
        /// Assert nodes in seed districts 0 and 1 are the same as the python implementation
        /// </summary>
        [Fact]
        public void PartitonSubgraphTest1()
        {
            var path = new Paths();
            var filePath = System.IO.Path.Combine(path.executingAssemblyDir, "al_vtds20_with_seeds.json");
            Partition p = new Partition(filePath, "CD_Seed", "TOTPOP", new string[] { "VAP", "BVAP" }, new Score[] { });
            int[] districts = { 0, 1 };
            var subgraph = p.DistrictSubGraph(districts.ToHashSet());
            Assert.Equal(395, subgraph.VertexCount);
        }
        /// <summary>
        /// Assert that the nodes assigned to set of districts is the same size as the generated subgraph for those districts.
        /// </summary>
        [Fact]
        public void PartitonSubgraphTest2()
        {
            var path = new Paths();
            var filePath = System.IO.Path.Combine(path.executingAssemblyDir, "al_vtds20_with_seeds.json");
            Partition p = new Partition(filePath, "CD_Seed", "TOTPOP", new string[] { "VAP", "BVAP" }, new Score[] { });
            int[] districts = { 1, 2 };
            var subgraph = p.DistrictSubGraph(districts.ToHashSet());
            var numNodes = p.Assignments.Where(x => districts.Contains(x)).Count();
            Assert.Equal(numNodes, subgraph.VertexCount);
        }

        /// <summary>
        /// Check Tally behavior
        /// </summary>
        [Fact]
        public void PartitionTallyTest1()
        {
            var path = new Paths();
            var filePath = System.IO.Path.Combine(path.executingAssemblyDir, "al_vtds20_with_seeds.json");
            var TallyBVAP = Scores.TallyFactory("BVAP", "BVAP");
            Partition p = new Partition(filePath, "CD_Seed", "TOTPOP", new string[] {"VAP", "BVAP"}, new Score[] {TallyBVAP});
            var pops = (DistrictWideScoreValue) p.Score("BVAP");
            Assert.Equal((double) 73218, pops.Value.Min());
        }
    }
}
