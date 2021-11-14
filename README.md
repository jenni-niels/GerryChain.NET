# GerryChain.NET

A .NET implementation of the ReCom Markov chain for redistricting.  Development goals are fast heuristic optimization 

## TODO::
* ~~Lazy evaluated score on Partition objects~~
* ~~Decision on using Quikgraph or "homebrewed" immutable graph representation?~~ - Using Quikgraph
* ~~Add grid graph toy example~~
* ~~Add unit test for initial graph representation~~
* ~~Add json Parser for networkx format~~

* Score to make factories for:
    * ~~Tally~~
    * CutEdges
    * Election Results
    * Partisan Metrics
        * Mean Median
        * Efficiency Gap
        * Eguia's Metric

* Add Chain class
    * ReCom step function (with parallelized proposal generation)
    * Neutral ensemble sample
    * County Aware
* Add Optimization chain class
    * Objective function representation.
    * Short Bursts
    * Modified Hill Climbing


## Future Goal
* Nuget Package
* Parallelization of ReCom sampling and optimization