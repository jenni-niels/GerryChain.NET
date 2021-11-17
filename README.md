# GerryChain.NET

A .NET implementation of the ReCom Markov chain for redistricting.  Development goals are fast heuristic optimization 

## TODO::
* ~~Lazy evaluated score on Partition objects~~
* ~~Decision on using Quikgraph or "homebrewed" immutable graph representation?~~ - Using Quikgraph
* ~~Add grid graph toy example~~
* ~~Add unit test for initial graph representation~~
* ~~Add json Parser for networkx format~~
* Region aware support seems to have decreased performance by an order of magnitude.  Investigate further.


* Score to make factories for:
    * ~~Tally~~
    * ~~CutEdges~~
    * Election Results
    * Partisan Metrics
        * Mean Median
        * Efficiency Gap
        * Eguia's Metric

* Add Chain class
    * ~~ReCom step function (with parallelized proposal generation)~~
    * ~~Neutral ensemble sample~~
    * ~~Add support for County Aware chains.  But add region crossing tag to graph nodes that encode the scale of the edge weight.~~

* Add Optimization chain class
    * Objective function representation.
    * Short Bursts
    * Modified Hill Climbing

* Create ReCom/Chain interface and restructure proposal generation to implement with support for multiple ReCom variants.
    * Currently
        * ReCom vanilla (cut edge)
        * ReCom vanilla (district pairs)
        * Reversible ReCom


## Future Goals
* Nuget Package
* Heuristic Optimization Functionality