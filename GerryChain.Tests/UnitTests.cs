using System;
using Xunit;

namespace GerryChain.Tests
{
    public class UnitTests
    {
        [Fact]
        public void Test1()
        {
            GerryChain.DualGraph d = GerryChain.DualGraph.GridGraph(5,5);
            Assert.Equal((double) (5*5), d.TotalPop);
        }
    }
}
