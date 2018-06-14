using System;
using System.Collections.Generic;
using System.Linq;

namespace Solve
{
	public interface IGenomeFitness<TGenome> : IComparable<IGenomeFitness<TGenome>>, IEquatable<IGenomeFitness<TGenome>>
		where TGenome : IGenome
	{
		TGenome Genome { get; }
		IFitness Fitness { get; }
	}

	public interface IGenomeFitness<TGenome, TFitness> : IGenomeFitness<TGenome>, IComparable<IGenomeFitness<TGenome, TFitness>>, IEquatable<IGenomeFitness<TGenome, TFitness>>
		where TGenome : IGenome
		where TFitness : IFitness
	{

		new TFitness Fitness { get; }
	}


	public struct GenomeFitness<TGenome, TFitness> : IGenomeFitness<TGenome, TFitness>
		where TGenome : IGenome
		where TFitness : IFitness
	{
		public TGenome Genome { get; private set; }
		public TFitness Fitness { get; private set; }

		IFitness IGenomeFitness<TGenome>.Fitness => this.Fitness;

		public GenomeFitness(TGenome genome, TFitness Fitness)
		{
			this.Genome = genome;
			this.Fitness = Fitness;
		}

		public int CompareTo(IGenomeFitness<TGenome, TFitness> other)
			=> GenomeFitness.Comparison(this, other);

		public bool Equals(IGenomeFitness<TGenome, TFitness> other)
			=> Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);

		int IComparable<IGenomeFitness<TGenome>>.CompareTo(IGenomeFitness<TGenome> other)
			=> GenomeFitness.Comparison(this, other);

		bool IEquatable<IGenomeFitness<TGenome>>.Equals(IGenomeFitness<TGenome> other)
			=> Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);

		int IComparable<IGenomeFitness<TGenome, TFitness>>.CompareTo(IGenomeFitness<TGenome, TFitness> other)
			=> GenomeFitness.Comparison(this, other);

		bool IEquatable<IGenomeFitness<TGenome, TFitness>>.Equals(IGenomeFitness<TGenome, TFitness> other)
			=> Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);

		public static implicit operator GenomeFitness<TGenome, TFitness>(KeyValuePair<TGenome, TFitness> input)
			=> new GenomeFitness<TGenome, TFitness>(input.Key, input.Value);

		public static implicit operator GenomeFitness<TGenome, TFitness>((TGenome, TFitness) input)
			=> new GenomeFitness<TGenome, TFitness>(input.Item1, input.Item2);

		public static implicit operator (TGenome, TFitness) (GenomeFitness<TGenome, TFitness> input)
			=> (input.Genome, input.Fitness);

		public static implicit operator KeyValuePair<TGenome, TFitness>(GenomeFitness<TGenome, TFitness> input)
			=> new GenomeFitness<TGenome, TFitness>(input.Genome, input.Fitness);
	}

	public struct GenomeFitness<TGenome> : IGenomeFitness<TGenome>
		where TGenome : IGenome
	{
		public TGenome Genome { get; private set; }
		public IFitness Fitness { get; private set; }

		public GenomeFitness(TGenome genome, IFitness Fitness)
		{
			this.Genome = genome;
			this.Fitness = Fitness;
		}
		public int CompareTo(IGenomeFitness<TGenome> other)
			=> GenomeFitness.Comparison(this, other);

		public bool Equals(IGenomeFitness<TGenome> other)
			=> Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);

		public int CompareTo(IGenomeFitness<TGenome, IFitness> other)
			=> GenomeFitness.Comparison(this, other);

		public bool Equals(IGenomeFitness<TGenome, IFitness> other)
			=> Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);
	}

	public static class GenomeFitness
	{

		public static bool IsSuperiorTo<TGenome>(this IGenomeFitness<TGenome> x, IGenomeFitness<TGenome> y)
			where TGenome : IGenome
		{
			return Comparison(x, y) == Fitness.ORDER_DIRECTION;
		}

		static int Comparison<TGenome>(TGenome xG, IFitness xF, TGenome yG, IFitness yF)
			where TGenome : IGenome
		{
			int c = Fitness.ValueComparison(xF, yF);
			//#if DEBUG
			//			int c2 = Fitness.ValueComparison(yF, xF);
			//			Debug.Assert(c == -c2, "Value comparison failed symmetry.");
			//#endif
			if (c != 0) return c;
			if (xG.Hash == yG.Hash) return 0;

			var xLen = xG.Length;
			var yLen = yG.Length;
			// Smaller is better...
			if (xLen < yLen) return +Fitness.ORDER_DIRECTION;
			if (xLen > yLen) return -Fitness.ORDER_DIRECTION;

			return Fitness.IdComparison(xF, yF);
		}

		public static int Comparison<TGenome>(IGenomeFitness<TGenome> x, IGenomeFitness<TGenome> y)
			where TGenome : IGenome
			=> Comparison(x.Genome, x.Fitness, y.Genome, y.Fitness);

		public static int Comparison<TGenome, TFitness>(KeyValuePair<TGenome, TFitness> x, KeyValuePair<TGenome, TFitness> y)
			where TGenome : IGenome
			where TFitness : IFitness
			=> Comparison(x.Key, x.Value, y.Key, y.Value);

		public static int Comparison<TGenome, TFitness>((TGenome, TFitness) x, (TGenome, TFitness) y)
			where TGenome : IGenome
			where TFitness : IFitness
			=> Comparison(x.Item1, x.Item2, y.Item1, y.Item2);

		public static IGenomeFitness<TGenome>[] Sort<TGenome>(this IGenomeFitness<TGenome>[] target, bool useSnapShots = false)
			where TGenome : IGenome
		{
			if (useSnapShots)
			{
				var len = target.Length;
				var snapshotMap = target.ToDictionary(gf => (gf.Genome, gf.Fitness.SnapShot()), gf => gf);
				var temp = snapshotMap.Keys.ToArray();
				Array.Sort(temp, Comparison);
				for (var i = 0; i < len; i++)
					target[i] = snapshotMap[temp[i]];
			}
			else
			{
				Array.Sort(target, Comparison);
			}

			return target;
		}

		public static KeyValuePair<TGenome, TFitness>[] Sort<TGenome, TFitness>(this KeyValuePair<TGenome, TFitness>[] target, bool useSnapShots = false)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			if (useSnapShots)
			{
				var len = target.Length;
				var snapshotMap = target.ToDictionary(gf => (gf.Key, gf.Value.SnapShot()), gf => gf);
				var temp = snapshotMap.Keys.ToArray();
				Array.Sort(temp, Comparison);
				for (var i = 0; i < len; i++)
					target[i] = snapshotMap[temp[i]];
			}
			else
			{
				Array.Sort(target, Comparison);
			}

			return target;
		}

		public static (TGenome, TFitness)[] Sort<TGenome, TFitness>(this (TGenome Genome, TFitness Fitness)[] target, bool useSnapShots = false)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			if (useSnapShots)
			{
				var len = target.Length;
				var snapshotMap = target.ToDictionary(gf => (gf.Genome, gf.Fitness.SnapShot()), gf => gf);
				var temp = snapshotMap.Keys.ToArray();
				Array.Sort(temp, Comparison);
				for (var i = 0; i < len; i++)
					target[i] = snapshotMap[temp[i]];
			}
			else
			{
				Array.Sort(target, Comparison);
			}

			return target;
		}


		public static IOrderedEnumerable<IGenomeFitness<TGenome>> Sorted<TGenome>(this IEnumerable<IGenomeFitness<TGenome>> target)
			where TGenome : IGenome
			=> target.OrderBy(g => g, Comparer<TGenome>.Instance);

		public class Comparer<TGenome> : IComparer<IGenomeFitness<TGenome>>
			where TGenome : IGenome
		{
			public int Compare(IGenomeFitness<TGenome> x, IGenomeFitness<TGenome> y)
			{
				return Comparison(x, y);
			}

			public static readonly Comparer<TGenome> Instance = new Comparer<TGenome>();
		}

		public class Comparer<TGenome, TFitness> : IComparer<IGenomeFitness<TGenome, TFitness>>
			where TGenome : IGenome
			where TFitness : IFitness
		{
			public int Compare(IGenomeFitness<TGenome, TFitness> x, IGenomeFitness<TGenome, TFitness> y)
			{
				return Comparison(x, y);
			}

			public static readonly Comparer<TGenome, TFitness> Instance = new Comparer<TGenome, TFitness>();
		}


		public static GenomeFitness<TGenome> SnapShot<TGenome>(this IGenomeFitness<TGenome> source)
			where TGenome : IGenome
		{
			return new GenomeFitness<TGenome>(source.Genome, source.Fitness.SnapShot());
		}

		public static GenomeFitness<TGenome> SnapShot<TGenome>(this (TGenome Genome, IFitness Fitness) source)
			where TGenome : IGenome
		{
			return new GenomeFitness<TGenome>(source.Genome, source.Fitness.SnapShot());
		}

		public static GenomeFitness<TGenome, TFitness> New<TGenome, TFitness>(TGenome genome, TFitness fitness)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			return new GenomeFitness<TGenome, TFitness>(genome, fitness);
		}

		public static List<GenomeFitness<TGenome>> Pareto<TGenome, TFitness>(
			this IEnumerable<KeyValuePair<TGenome, TFitness>> population)
			where TGenome : IGenome
			where TFitness : IFitness
			=> Pareto(population.Select(gf => (IGenomeFitness<TGenome>)new GenomeFitness<TGenome>(gf.Key, gf.Value)));

		public static List<GenomeFitness<TGenome>> Pareto<TGenome, TFitness>(
			this IEnumerable<(TGenome Genome, IFitness Fitness)> population)
			where TGenome : IGenome
			where TFitness : IFitness
			=> Pareto(population.Select(gf => (IGenomeFitness<TGenome>)new GenomeFitness<TGenome>(gf.Genome, gf.Fitness)));

		public static List<GenomeFitness<TGenome>> Pareto<TGenome>(
			this IEnumerable<IGenomeFitness<TGenome>> population)
			where TGenome : IGenome
		{
			if (population == null)
				throw new ArgumentNullException(nameof(population));

			var d = new Dictionary<string, GenomeFitness<TGenome>>();
			foreach (var entry in population.OrderBy(g => g)) // Enforce distinct by ordering.
			{
				var key = entry.Genome.Hash;
				if (!d.ContainsKey(key)) d.Add(key, entry.SnapShot());
			}

			bool found;
			List<GenomeFitness<TGenome>> p;
			do
			{
				found = false;
				var values = d.Select(kvp => kvp.Value);
				p = values.ToList();
				foreach (var g in p)
				{
					var gs = g.Fitness.Scores.ToArray();
					var len = gs.Length;
					if (values.Any(o =>
						 {
							 var os = o.Fitness.Scores.ToArray();
							 for (var i = 0; i < len; i++)
							 {
								 var osv = os[i];
								 if (double.IsNaN(osv)) return true;
								 if (gs[i] <= os[i]) return false;
							 }
							 return true;
						 }))
					{
						found = true;
						d.Remove(g.Genome.Hash);
					}
				}
			}
			while (found);

			return p;
		}


	}
}
