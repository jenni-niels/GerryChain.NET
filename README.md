# GerryChain.NET

A .NET implementation of the ReCom Markov chain for redistricting.  Development goals are fast heuristic optimization 

## TODO::
* Lazy evaluated score on Partition objects
* Decision on using Quikgraph or "homebrewed" immutable graph representation?
* Add grid graph toy example
* Add unit test for initial graph representation

* Add Chain class
    * ReCom step function (with parallelized proposal generation)
    * Neutral ensemble sample
* Add Optimization chain class
    * Objective function representation.
    * Short Bursts
    * Modified Hill


## Future Goals
* Nuget Package
* Parallelization of ReCom sampling and optimzation