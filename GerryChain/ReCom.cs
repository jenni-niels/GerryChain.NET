using System;
using System.Collections.Generic;


namespace GerryChain
{
    public record Proposal(Partition Part, (int, int) DistrictsAffected , Dictionary<int, int> Flips);
}