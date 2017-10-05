using Open.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Solve
{
	public abstract class ProblemBase<TGenome> : IProblem<TGenome>
	where TGenome : class, IGenome
	{

		protected readonly ConcurrentDictionary<string, Lazy<GenomeFitness<TGenome, Fitness>>>
			Fitnesses = new ConcurrentDictionary<string, Lazy<GenomeFitness<TGenome, Fitness>>>();

		protected readonly ConcurrentHashSet<string>
			Rejects = new ConcurrentHashSet<string>();


		static int ProblemCount = 0;
		readonly int _id = Interlocked.Increment(ref ProblemCount);
		public int ID { get { return _id; } }

		long _testCount = 0;
		public long TestCount
		{
			get
			{
				return _testCount;

			}
		}


		protected ProblemBase()
		{

		}


		// Override this if there is a common key for multiple genomes (aka they are equivalient).
		protected virtual TGenome GetFitnessForKeyTransform(TGenome genome)
		{
			return genome;
		}

		public bool TryGetFitnessFor(TGenome genome, out GenomeFitness<TGenome, Fitness> fitness)
		{
			genome = GetFitnessForKeyTransform(genome);
			var key = genome.Hash;

			if (Fitnesses.TryGetValue(key, out Lazy<GenomeFitness<TGenome, Fitness>> value))
			{
				if(!Rejects.Contains(key))
				{
					fitness = value.Value;
					return true;
				}
			}

			fitness = default(GenomeFitness<TGenome, Fitness>);
			return false;
		}

		public GenomeFitness<TGenome, Fitness>? GetFitnessFor(TGenome genome, bool ensureSourceGenome = false)
		{
			TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> gf);
			if (ensureSourceGenome && gf.Genome != genome) // Possible that a 'version' is stored.
				gf = new GenomeFitness<TGenome, Fitness>(genome, gf.Fitness);
			return gf;
		}

		public IEnumerable<IGenomeFitness<TGenome, Fitness>> GetFitnessFor(IEnumerable<TGenome> genomes, bool ensureSourceGenomes = false)
		{
			foreach (var genome in genomes)
			{
				if (TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> gf))
				{
					if (ensureSourceGenomes && gf.Genome != genome) // Possible that a 'version' is stored.
						gf = new GenomeFitness<TGenome, Fitness>(genome, gf.Fitness);
					yield return gf;
				}
			}
		}

		public GenomeFitness<TGenome, Fitness> GetOrCreateFitnessFor(TGenome genome)
		{
			if (!genome.IsReadOnly)
				throw new InvalidOperationException("Cannot recall fitness for an unfrozen genome.");
			genome = GetFitnessForKeyTransform(genome);
			var key = genome.Hash;
			GenomeFitness<TGenome, Fitness> result = default(GenomeFitness<TGenome, Fitness>);
			Rejects.IfNotContains(key, () =>
			{
				result = Fitnesses
					.GetOrAdd(key, k =>
						Lazy.Create(() => GenomeFitness.New(genome, new Fitness()))).Value;
			});
			return result;
		}

		// Presents a means for removing and blocking future storage of (for memory control).
		public void Reject(string hash)
		{
			Rejects.Add(hash);
			Fitnesses.TryRemove(hash);
		}

		public void Reject(IEnumerable<string> hashes)
		{
			foreach (var hash in hashes)
				Reject(hash);
		}

		public bool WasRejected(string hash)
		{
			return Rejects.Contains(hash);
		}

		public async Task<IFitness> ProcessTest(TGenome g, long sampleId = 0, bool mergeWithGlobal = false)
		{
			var f = new Fitness();
			if (sampleId == 0)
			{
				var global = GetOrCreateFitnessFor(g).Fitness;
				using (await global.Lock.LockAsync())
				{
					await ProcessTest(g, f, -global.SampleCount, true);
					if (mergeWithGlobal) global.Merge(f);
				}
			}
			else
			{
				await ProcessTest(g, f, sampleId, true);
				if (mergeWithGlobal) AddToGlobalFitness(g, f);
			}

			Interlocked.Increment(ref _testCount);
			return f;
		}

		protected abstract Task ProcessTest(TGenome g, Fitness fitness, long sampleId, bool useAsync = true);

		GenomeTestDelegate<TGenome> _testProcessor;
		public GenomeTestDelegate<TGenome> TestProcessor
		{
			get
			{
				return LazyInitializer.EnsureInitialized(ref _testProcessor, () => ProcessTest);
			}
		}

		public void AddToGlobalFitness<T>(IEnumerable<T> results) where T : IGenomeFitness<TGenome>
		{
			foreach (var r in results)
				AddToGlobalFitness(r);
		}

		public IFitness AddToGlobalFitness(IGenomeFitness<TGenome> result)
		{
			return AddToGlobalFitness(result.Genome, result.Fitness);
		}

		public IFitness AddToGlobalFitness(TGenome genome, IFitness fitness)
		{
			IFitness result = fitness;
			Rejects.IfNotContains(genome.Hash, () =>
			{
				var global = GetOrCreateFitnessFor(genome).Fitness;
				if (global == fitness)
					throw new InvalidOperationException("Adding fitness on to itself.");
				global.Merge(fitness);
				result = global.SnapShot();
			});
			return fitness;
		}

		public int GetSampleCountFor(TGenome genome)
		{
			return TryGetFitnessFor(genome, out GenomeFitness<TGenome, Fitness> fitness)
                ? fitness.Fitness.SampleCount
                : 0;
		}
	}
}