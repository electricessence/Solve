# Solve: Processing Schemes

## Concepts

### Problems

A "Problem" is a unique challenge that can be addressed by a genome within an environment.

#### Problem Pools

A "Problem Pool" is a specific set of fitness metrics that can be applied to a specific problem. 

## Challenges

Unlike a classical Genetic/Evolutionary Algorithm, these schemes can process multi-dimensional fitnesses while simultaneously solving muliptle problems.

The goal is to potentially provide completely different sets (or priorities) of fitness to tackle completely sepearate problems but never process the same genome/sample twice.

### Reliability

Whatever algorithm is chosen, must be reliable.  It must produce reliable results.

### Efficiency

Avoiding wasted processes is an obvious goal.  But it quite common in classical algorithms that may repeat samples with the same genome over and over.

### Performance

Contention, multi-threading, and a suite of other concepts affect how well these algorithms perform.

## The Tower

The default scheme is a 'Tower' where each genome is presented consistent sample at each level as to measure their fitness against that sample.

The champions of that level are immediately promoted to the next level in that tower.  The losers have been tested as 'not as good as another' at that level and need not be resampled with their "level fitness" intact.  They can be ranked against eachother to decide who should proceed first.

Losers should eventually proceed to the next level (at a slower rate than champions) to ensure that the level they lost at wasn't simply a fluke and that potentially they might be the best of all simply with that as an outlier.

### Tower Performance

The throughput of the Tower Scheme is based on many factors.  Examples of different towers are provided in order to understand the process and these challenges.
