using System;
using Xunit;

namespace GerryChain.Tests
{
    public class UnitTests
    {
        [Fact]
        public void DualGraphTest1()
        {
            GerryChain.DualGraph d = GerryChain.DualGraph.GridGraph(5,5);
            Assert.Equal((double) (5*5), d.TotalPop);
        }
        [Fact]
        public void PartitionTest1()
        {
            GerryChain.Partition p = new GerryChain.Partition("../resources/al_vtds_w_pop.json", "CD_Seed", "TOTPOP", new string[] {"VAP"});
            Assert.False(p.HasParent);
        }
    }
}
