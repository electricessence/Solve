/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: Apache https://github.com/electricessence/Solve/blob/master/LICENSE.txt
 */

using Open.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Solve
{
	/// <summary>
	/// Problems define what parameters need to be tested to resolve fitness.
	/// </summary>
	public interface IProblem<TGenome>
		 where TGenome : IGenome
	{
		int ID { get; }

		// 0 = acquire sampleId from sample count.  Negative numbers are allowed.
		Task<IFitness> ProcessTest(TGenome g, long sampleId = 0, bool mergeWithGlobal = false);

		// Alternative for delegate usage.
		GenomeTestDelegate<TGenome> TestProcessor { get; }

		void AddToGlobalFitness<T>(IEnumerable<T> results) where T : IGenomeFitness<TGenome>;
		IFitness AddToGlobalFitness(IGenomeFitness<TGenome> result);
		IFitness AddToGlobalFitness(TGenome genome, IFitness result);

		GenomeFitness<TGenome, Fitness>? GetFitnessFor(TGenome genome, bool ensureSourceGenome = false);
		IEnumerable<IGenomeFitness<TGenome, Fitness>> GetFitnessFor(IEnumerable<TGenome> genome, bool ensureSourceGenomes = false);

		int GetSampleCountFor(TGenome genome);

		void Reject(string hash);
		void Reject(IEnumerable<string> hashes);
		bool WasRejected(string hash);
	}

	public static class ProblemExtensions
	{

		public static Task<(IProblem<TGenome> Problem, IFitness Fitness)[]> ProcessOnce<TGenome>(
			this IEnumerable<IProblem<TGenome>> problems,
			TGenome genome,
			long sampleId = 0,
			bool mergeWithGlobal = true)
			where TGenome : IGenome
		{
			if (genome == null)
				throw new ArgumentNullException(nameof(genome));
			if (!problems.HasAny())
				return Task.FromResult(Array.Empty<(IProblem<TGenome> Problem, IFitness Fitness)>());

			return Task.WhenAll(
				problems.Select(p =>
					p.ProcessTest(genome, sampleId, mergeWithGlobal)
						.ContinueWith(t => (p, t.Result))
				)
			);
		}

		public static Task<(IProblem<TGenome> Problem, IGenomeFitness<TGenome>[] Results)[]> ProcessOnce<TGenome>(
			this IEnumerable<IProblem<TGenome>> problems,
			IEnumerable<TGenome> genomes,
			long sampleId = 0,
			bool mergeWithGlobal = false)
			where TGenome : IGenome
		{
			if (genomes == null)
				throw new ArgumentNullException(nameof(genomes));
			if (!problems.HasAny())
				return Task.FromResult(Array.Empty<(IProblem<TGenome> Problem, IGenomeFitness<TGenome>[] Results)>());

			return Task.WhenAll(problems
				.Select(p =>
					Task.WhenAll(
						genomes.Select(g => p.ProcessTest(g, sampleId, mergeWithGlobal)
							.ContinueWith(t => (IGenomeFitness<TGenome>)new GenomeFitness<TGenome>(g, t.Result))))
						.ContinueWith(t => (p, t.Result.Sort()))));
		}


		public static Task<(IProblem<TGenome> Problem, IFitness Fitness)[]> Process<TGenome>(
			this IEnumerable<IProblem<TGenome>> problems,
			TGenome genome,
			IEnumerable<long> sampleIds,
			bool mergeWithGlobal = false)
			where TGenome : IGenome
		{
			if (genome == null)
				throw new ArgumentNullException(nameof(genome));
			if (!problems.HasAny())
				return Task.FromResult(Array.Empty<(IProblem<TGenome> Problem, IFitness Fitness)>());

			return Task.WhenAll(
				problems.Select(
					p => Task.WhenAll(sampleIds.Select(id => p.ProcessTest(genome, id, id >= 0)))
						.ContinueWith(t =>
						{
							var f = (IFitness)t.Result.Merge();
							var kvp = (p, f);
							if (mergeWithGlobal) p.AddToGlobalFitness(genome, f);
							return kvp;
						})));
		}


		public static Task<(IProblem<TGenome> Problem, IGenomeFitness<TGenome>[] Results)[]> Process<TGenome>(
			this IEnumerable<IProblem<TGenome>> problems,
			IEnumerable<TGenome> genomes,
			IEnumerable<long> sampleIds,
			bool mergeWithGlobal = false)
			where TGenome : IGenome
		{
			if (genomes == null)
				throw new ArgumentNullException(nameof(genomes));
			if (!problems.HasAny())
				return Task.FromResult(Array.Empty<(IProblem<TGenome>, IGenomeFitness<TGenome>[])>());

			return Task.WhenAll(problems.Select(
				p => Task.WhenAll(genomes.Select(
					g => Task.WhenAll(sampleIds.Select(id => p.ProcessTest(g, id)))
						.ContinueWith(t =>
						{
							var f = t.Result.Merge();
							IGenomeFitness<TGenome> gf = new GenomeFitness<TGenome>(g, f);
							if (mergeWithGlobal) p.AddToGlobalFitness(g, f);
							return gf;
						})))
						.ContinueWith(t => (p, t.Result.Sort()))));
		}


		public static Task<(IProblem<TGenome> Problem, IGenomeFitness<TGenome>[] Results)[]> Process<TGenome>(
			this IEnumerable<IProblem<TGenome>> problems,
			IEnumerable<TGenome> genomes,
			int count = 1,
			bool mergeWithGlobal = false)
			where TGenome : IGenome
		{
			return Process(problems, genomes, Enumerable.Range(0, count).Select(i => SampleID.Next()), mergeWithGlobal);
		}

		public static Task<(IProblem<TGenome> Problem, IFitness Fitness)[]> Process<TGenome>(
		   this IEnumerable<IProblem<TGenome>> problems,
		   TGenome genome,
		   int count = 1,
		   bool mergeWithGlobal = false)
		   where TGenome : IGenome
		{
			return Process(problems, genome, Enumerable.Range(0, count).Select(i => SampleID.Next()), mergeWithGlobal);
		}

		public static Task<(IProblem<TGenome> Problem, IGenomeFitness<TGenome>[] Results)> Process<TGenome>(
			this IProblem<TGenome> problem,
			IEnumerable<TGenome> genomes,
			int count = 1,
			bool mergeWithGlobal = false)
			where TGenome : IGenome
		{
			if (problem == null)
				throw new ArgumentNullException(nameof(problem));
			if (genomes == null)
				throw new ArgumentNullException(nameof(genomes));
			return Process(new IProblem<TGenome>[] { problem }, genomes, Enumerable.Range(0, 1).Select(i => SampleID.Next()), mergeWithGlobal)
				.ContinueWith(t => t.Result.Single());
		}

	}

}
