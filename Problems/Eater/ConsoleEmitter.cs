using Open.Collections;
using Open.Numeric;
using Open.Threading;
using Solve;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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
			var genome = kvp.Value;
			var result = new Fitness(FullTests.GetOrAdd(genome.Hash, key => Problem.Samples.TestAll(key))).SnapShot();

			ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => !LastScore.HasValue || LastScore.Value < result || LastHash == genome.Hash,
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						var ls = LastScore;

						EmitTopGenomeStats(kvp);
						LastScore = result;

						if (ls.HasValue && ls.Value < result) Console.WriteLine("New winner ^^^.");
					}));
		}

		public void EmitTopGenomeStats(KeyValuePair<IProblem<EaterGenome>, EaterGenome> kvp)
		{
			var p = kvp.Key;
			var genome = kvp.Value;
			var fitness = p.GetFitnessFor(genome).Value.Fitness.SnapShot();

			var asReduced = genome.AsReduced();
			ThreadSafety.LockConditional(
				SynchronizedConsole.Sync,
				() => fitness.SampleCount > SampleMinimum && (!LastScore.HasValue || LastScore.Value.Count < fitness.Count || LastScore.Value < fitness || LastHash == genome.Hash),
				() => SynchronizedConsole.OverwriteIfSame(ref LastTopGenomeUpdate, () => LastHash == genome.Hash,
					cursor =>
					{
						if (asReduced == genome)
							Console.WriteLine("{0}:\t{1}", p.ID, genome.Hash);
						else
							Console.WriteLine("{0}:\t{1}\n=>\t{2}", p.ID, genome.Hash, asReduced.Hash);

						EmitFitnessScoreWithLabels(fitness);
						Console.WriteLine();

						LastScore = fitness.SnapShot();
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
