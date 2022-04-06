# GerryChain.NET

A .NET implementation of the ReCom Markov chain for redistricting, with parallelized proposal generation.
Development goals target fast and flexible heuristic optimization.

[![NuGet Status](https://img.shields.io/nuget/v/GerryChain.svg)](https://www.nuget.org/packages/GerryChain)

## TODO::

* Implement flips Proposal version of PCompress.Replayer

* Create example scripts for:
    * ~~Optimization~~
    * ~~PCompress~~
    * Displacement
    * VRA Scores

* Scores to make factories for:
    * ~~Tally~~
    * ~~CutEdges~~
    * ~~Election Results~~
    * Partisan Metrics
        * Mean Median
        * Efficiency Gap
        * Eguia's Metric

* Add Proposal Generators
    * ReCom
        * ~~ReCom vanilla (cut edge)~~
        * ReCom vanilla (district pairs)
        * Reversible ReCom
    * Flip chain
    * Swap chain

* ~~Lazy evaluated score on Partition objects~~
* ~~Decision on using Quikgraph or "homebrewed" immutable graph representation?~~ - Using Quikgraph
* ~~Add grid graph toy example~~
* ~~Add unit test for initial graph representation~~
* ~~Add json Parser for networkx format~~

* ~~Create ReCom/Chain interface and restructure proposal generation to implement with support for multiple ReCom variants.~~

* ~~Add Chain class~~
    * ~~ReCom step function (with parallelized proposal generation)~~
    * ~~Neutral ensemble sample~~
    * ~~Add support for County Aware chains.  But add region crossing tag to graph nodes that encode the scale of the edge weight.~~

* ~~Add Optimization chain class~~
    * ~~Objective function representation~~.
    * ~~Short Bursts~~
    * ~~Modified Hill Climbing~~