using Open.Collections;
using Open.Numeric;
using Open.Threading;
using Solve;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using KVP = Open.Collections.KeyValuePair;

namespace Eater
{
	public class ConsoleEmitter
	{
		public uint SampleMinimum;

		public ConsoleEmitter(uint sampleMinimum = 50)
		{
			SampleMinimum = sampleMinimum;
		}

		readonly ConcurrentDictionary<string, ProcedureResult[]> FullTests = new ConcurrentDictionary<string, ProcedureResult[]>();

		public FitnessScore? LastScore;
		public string LastHash;
		public SynchronizedConsole.Message LastTopGenomeUpdate;

		public void EmitTopGenomeFullStats(KeyValuePair<IProblem<EaterGenome>, EaterGenome> kvp)
		{
			EmitTopGenomeFullStats(kvp.Key, kvp.Value);
		}

		public void EmitTopGenomeFullStats(IProblem<EaterGenome> p, EaterGenome genome)
		{
			EmitTopGenomeStatsInternal(p, genome, new Fitness(FullTests.GetOrAdd(genome.Hash, key => Problem.Samples.TestAll(key))));
		}

		public void EmitTopGenomeStats(KeyValuePair<IProblem<EaterGenome>, EaterGenome> kvp)
		{
			EmitTopGenomeStatsInternal(kvp.Key, kvp.Value);
		}

		public void EmitTopGenomeStats(IProblem<EaterGenome> p, EaterGenome genome)
		{
			EmitTopGenomeStatsInternal(p,genome);
		}

		protected bool EmitTopGenomeStatsInternal(KeyValuePair<IProblem<EaterGenome>, EaterGenome> kvp, IFitness fitness = null)
		{
			return EmitTopGenomeStatsInternal(KVP.Create( kvp.Key, kvp.Value ));
		}

		protected bool EmitTopGenomeStatsInternal(IProblem<EaterGenome> p, EaterGenome genome, IFitness fitness = null)
		{
			var f = (fitness ?? p.GetFitnessFor(genome).Value.Fitness).SnapShot();

			var asReduced = genome.AsReduced();
			return ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => f.SampleCount > SampleMinimum && (!LastScore.HasValue || LastScore.Value < f || LastHash == genome.Hash),
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						if (asReduced == genome)
							Console.WriteLine("{0}:\t{1}", p.ID, genome.Hash);
						else
							Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.Hash, asReduced.Hash);

						EmitFitnessScoreWithLabels(f);
						Console.WriteLine();

						LastScore = f;
						LastHash = genome.Hash;
					}));
		}

		public void EmitFitnessScoreWithLabels(IFitness fitness)
		{
			var scoreStrings = new List<string>();
			var scores = fitness.Scores.ToArray();
			var len = scores.Length;
			for (var i = 0; i < len; i++)
			{
				scoreStrings.Add(String.Format(ProblemFragmented.FitnessLabels[i], scores[i]));
			}

			Console.WriteLine("  \t{0} ({1:n0} samples)", scoreStrings.JoinToString(", "), fitness.SampleCount);
		}

	}
}
