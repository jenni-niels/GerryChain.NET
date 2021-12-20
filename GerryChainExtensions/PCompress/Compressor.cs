using System;
using System.Collections.Generic;
using System.Linq;
using GerryChain;

namespace PCompress
{
    public class Record
    {
        public Chain MarkovChain { get; init; }
    }

    public class Replay : IEnumerable<Partition>
    {
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<Partition> GetEnumerator()
        {
            return new ChainReplayer(this);
        }

        public class ChainReplayer : IEnumerator<Partition>
        {
        }
    }
}