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
		{
			return GenomeFitness.Comparison(this, other);
		}

		public bool Equals(IGenomeFitness<TGenome, TFitness> other)
		{
			return Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);
		}

		int IComparable<IGenomeFitness<TGenome>>.CompareTo(IGenomeFitness<TGenome> other)
		{
			return GenomeFitness.Comparison(this, other);
		}

		bool IEquatable<IGenomeFitness<TGenome>>.Equals(IGenomeFitness<TGenome> other)
		{
			return Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);
		}

		int IComparable<IGenomeFitness<TGenome, TFitness>>.CompareTo(IGenomeFitness<TGenome, TFitness> other)
		{
			return GenomeFitness.Comparison(this, other);
		}

		bool IEquatable<IGenomeFitness<TGenome, TFitness>>.Equals(IGenomeFitness<TGenome, TFitness> other)
		{
			return Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);
		}
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
		{
			return GenomeFitness.Comparison(this, other);
		}

		public bool Equals(IGenomeFitness<TGenome> other)
		{
			return Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);
		}

		public int CompareTo(IGenomeFitness<TGenome, IFitness> other)
		{
			return GenomeFitness.Comparison(this, other);
		}

		public bool Equals(IGenomeFitness<TGenome, IFitness> other)
		{
			return Genome.Equals(other.Genome) && Fitness.Equals(other.Fitness);
		}
	}

	public static class GenomeFitness
	{

		public static bool IsGreaterThan<TGenome>(this IGenomeFitness<TGenome> x, IGenomeFitness<TGenome> y)
			where TGenome : IGenome
		{
			return Comparison(x, y) == Fitness.ORDER_DIRECTION;
		}

		public static int Comparison<TGenome>(IGenomeFitness<TGenome> x, IGenomeFitness<TGenome> y)
			where TGenome : IGenome
		{
			if (x == y) return 0;

			int c = Fitness.ValueComparison(x.Fitness, y.Fitness);
			if (c != 0) return c;

			var xLen = x.Genome.Hash.Length;
			var yLen = y.Genome.Hash.Length;
			// Smaller is better...
			if (xLen < yLen) return +Fitness.ORDER_DIRECTION;
			if (xLen > yLen) return -Fitness.ORDER_DIRECTION;

			return Fitness.IdComparison(x.Fitness, y.Fitness);
		}


		public static IGenomeFitness<TGenome>[] Sort<TGenome>(this IGenomeFitness<TGenome>[] target)
			where TGenome : IGenome
		{
			Array.Sort(target, Comparison);
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

		public static GenomeFitness<TGenome> SnapShot<TGenome, TFitness>(this IGenomeFitness<TGenome, TFitness> source)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			return new GenomeFitness<TGenome>(source.Genome, source.Fitness.SnapShot());
		}

		public static GenomeFitness<TGenome, TFitness> New<TGenome, TFitness>(TGenome genome, TFitness fitness)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			return new GenomeFitness<TGenome, TFitness>(genome, fitness);
		}

		internal static IGenomeFitness<TGenome, TFitness> GFFromGF<TGenome, TFitness>(this KeyValuePair<TGenome, TFitness> kvp)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			return new GenomeFitness<TGenome, TFitness>(kvp.Key, kvp.Value);
		}

		internal static IGenomeFitness<TGenome, TFitness> GFFromFG<TGenome, TFitness>(this KeyValuePair<TFitness, TGenome> kvp)
			where TGenome : IGenome
			where TFitness : IFitness
		{
			return new GenomeFitness<TGenome, TFitness>(kvp.Value, kvp.Key);
		}


		public static List<GenomeFitness<TGenome>> Pareto<TGenome, TFitness>(this IEnumerable<IGenomeFitness<TGenome, TFitness>> population)
			where TGenome : IGenome
			where TFitness : IFitness
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
