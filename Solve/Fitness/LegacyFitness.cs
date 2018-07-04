using Nito.AsyncEx;
using Open.Collections.Synchronized;
using Open.Numeric;
using Open.Numeric.Precision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Solve
{
	[DebuggerDisplay("Sum = {_result.Sum}, Count = {_result.Count}, Average = {_result.Average}")]
	public class SingleFitness : IComparable<SingleFitness>
	{
		public readonly double MaxScore;
		readonly object _sync = new Object();
		public SingleFitness(IEnumerable<double> scores = null, double maxScore = 1)
			: this(new ProcedureResult(0, 0), maxScore)
		{
			if (scores != null)
				Add(scores);
		}

		public SingleFitness(ProcedureResult initial, double maxScore = 1)
			: base()
		{
			Result = initial;
			MaxScore = maxScore;
		}

		public SingleFitness(double maxScore) : this(null, maxScore)
		{
		}


		public ProcedureResult Result { get; private set; }


		public void Add(double value, int count = 1)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative.");

			Debug.Assert(count > 0, "Must add a value greater than zero.");
			if (count != 0)
			{

				Debug.Assert(!double.IsNaN(value), "Adding a NaN value will completely invalidate the fitness value.");
				Debug.Assert(value <= MaxScore, "Adding a score that is above the maximum will potentially invalidate the current run.");
				// Ensures 1 update at a time.
				lock (_sync) Result = Result.Add(value, count);
			}
		}

		public void Add(ProcedureResult other)
		{
			Debug.Assert(!double.IsNaN(other.Average), "Adding a NaN value will completely invalidate the fitness value.");
			Debug.Assert(other.Average <= MaxScore, "Adding a score that is above the maximum will potentially invalidate the current run.");
			// Ensures 1 update at a time.
			lock (_sync) Result += other;
		}

		public void Add(IEnumerable<double> values)
		{
			double sum = 0;
			int count = 0;
			foreach (var value in values)
			{
				sum += value;
				count++;
			}
			Add(sum, count);
		}

		// Allow for custom comparison of individual fitness types.
		// By default, it' simply the average regardless of number of samples.
		public virtual int CompareTo(SingleFitness other)
		{
			if (this == other) return 0;
			if (other == null)
				throw new ArgumentNullException(nameof(other));

			// Check for weird averages that push the values above maximum and adjust.  (Bounce off the barrier.)   See above for debug assertions.

			var a = Result;
			if (a.Average > MaxScore)
				a = new ProcedureResult(MaxScore * a.Count, a.Count);
			var b = other.Result;
			if (b.Average > MaxScore)
				b = new ProcedureResult(MaxScore * b.Count, b.Count);

			return a.CompareTo(b);
		}

	}

	public interface IFitness : IComparable<IFitness>
	{
		bool HasSamples { get; } // Use for lightweight return.
		int SampleCount { get; }
		IReadOnlyList<double> Scores { get; }

		int Count { get; }

		long ID { get; }

		ProcedureResult GetResult(int index);
		double GetScore(int index);
	}

	[DebuggerDisplay("Count = {_results.Length}")]
	public class FitnessScore : IFitness
	{
		readonly ProcedureResult[] _results;

		public FitnessScore(IFitness source)
		{
			var len = source.Count;
			Count = len;
			ID = source.ID;
			SampleCount = source.SampleCount;
			var results = _results = new ProcedureResult[len];
			for (var i = 0; i < len; i++)
				results[i] = source.GetResult(i);
			_scores = Lazy.Create(() => results.Select(s => s.Average).ToList().AsReadOnly());
		}

		public bool HasSamples => Count != 0;

		public int Count
		{
			get;
			private set;
		}

		public long ID
		{
			get;
			private set;
		}

		public int SampleCount
		{
			get;
			private set;
		}

		Lazy<ReadOnlyCollection<double>> _scores;
		public IReadOnlyList<double> Scores
		{
			get
			{
				return _scores.Value;
			}
		}

		public int CompareTo(IFitness other)
		{
			return LegacyFitness.Comparison(this, other);
		}

		public ProcedureResult GetResult(int index)
		{
			return _results[index];
		}

		public double GetScore(int index)
		{
			return _results[index].Average;
		}

		public static bool operator >(FitnessScore a, IFitness b)
		{
			return a.CompareTo(b) * LegacyFitness.ORDER_DIRECTION == +1;
		}

		public static bool operator <(FitnessScore a, IFitness b)
		{
			return a.CompareTo(b) * LegacyFitness.ORDER_DIRECTION == -1;
		}

		public static bool operator >=(FitnessScore a, IFitness b)
		{
			return a.CompareTo(b) * LegacyFitness.ORDER_DIRECTION >= 0;
		}

		public static bool operator <=(FitnessScore a, IFitness b)
		{
			return a.CompareTo(b) * LegacyFitness.ORDER_DIRECTION <= 0;
		}
	}

	public class LegacyFitness : TrackedList<SingleFitness>, IFitness
	{

		public LegacyFitness() //: base(new AsyncReadWriteModificationSynchronizer())
		{
			ID = Interlocked.Increment(ref FitnessCount);
		}

		public LegacyFitness(IEnumerable<ProcedureResult> initial)
			: this()
		{
			// No synchronization needed.
			foreach (var i in initial)
				_source.Add(new SingleFitness(i));
		}

		/* 
		 * Concurrency rules dictate that if this is false, then there may still be a slight chance that it's in transition.
		 * If true, then it's assertive that samples have been added.
		 */
		bool _hasSamples = false;
		public bool HasSamples => _hasSamples || SampleCount != 0;

		public int SampleCount
		{
			get
			{
				if (Count == 0) return 0;
				return Sync.Reading(() => this.Min(s => s.Result.Count));
			}
		}

		public ProcedureResult GetResult(int index)
		{
			return this[index].Result;
		}

		public double GetScore(int index)
		{
			return GetResult(index).Average;
		}

		public IReadOnlyList<double> Scores
		{
			get
			{
				return Sync.Reading(() => this.Select(v => v.Result.Average).ToList())
					.AsReadOnly();
			}
		}

		protected override void OnModified()
		{
			_hasSamples = true;
			base.OnModified();
		}

		public void Add(ProcedureResult score)
		{
			Sync.Modifying(() =>
			{
				this.Add(new SingleFitness(score));
			});
		}

		public void AddTheseScores(IEnumerable<double> scores)
		{
			Sync.Modifying(() =>
			{
				var i = 0;
				var count = Count;
				foreach (var n in scores)
				{
					SingleFitness f;
					if (i < count)
					{
						f = this[i];
					}
					else
					{
						this.Add(f = new SingleFitness());
					}
					f.Add(n);
					i++;
				}
			});

		}

		public LegacyFitness Merge(IFitness other)
		{

			AssertIsAlive();

			if (other.Count != 0)
			{
				if (_source.Count != 0 && other.Count != Count)
					throw new InvalidOperationException("Cannot add fitness values where the count doesn't match.");

				Sync.Modifying(() =>
				{
					var count = other.Count;
					for (var i = 0; i < count; i++)
					{
						var r = other.GetResult(i);
						if (i < _source.Count) _source[i].Add(r);
						else _source.Add(new SingleFitness(r));
					}
				});
			}

			return this;
		}
		public void AddScores(params double[] scores)
		{
			AddTheseScores(scores);
		}

		// Allowing for a rejection count opens the possiblity for a second chance.

		int _rejectionCount = 0;
		public int RejectionCount
		{
			get
			{
				return _rejectionCount;
			}
			set
			{
				Interlocked.Exchange(ref _rejectionCount, value);
			}
		}

		public int IncrementRejection()
		{
			return Interlocked.Increment(ref _rejectionCount);
		}

		static long FitnessCount = 0;
		public long ID
		{
			get;
			private set;
		}

		// internal int TestingCount = 0;

		// Some cases enumerables are easier to sort in ascending than descending so "Top" in this respect means 'First'.
		public const int ORDER_DIRECTION = -1;
		public int CompareTo(IFitness other)
		{
			return Comparison(this, other);
		}

		public class Comparer : IComparer<IFitness>
		{
			public int Compare(IFitness x, IFitness y)
			{
				return Comparison(x, y);
			}
		}

		public static int Comparison(IFitness x, IFitness y)
		{
			int c;

			if (x == y) return 0;

			c = ValueComparison(x, y);
			if (c != 0) return c;

			c = SampleComparison(x, y);
			if (c != 0) return c;

			c = IdComparison(x, y);
			if (c != 0) return c;

			//Debug.Fail("Impossible? Interlocked failed?");
			return 0;
		}

		public static int ValueComparison(IFitness x, IFitness y)
		{
			if (x == y) return 0;
			if (x == null)
				throw new ArgumentNullException(nameof(x));
			if (y == null)
				throw new ArgumentNullException(nameof(y));
			int xLen = x.Count, yLen = y.Count;
			if (xLen != 0 || yLen != 0)
			{
				// Untested needs at least one test before being ordered.
				// It's also possible to fail adding because NaN so avoid.
				if (xLen == 0 && yLen != 0) return -ORDER_DIRECTION;
				if (xLen != 0 && yLen == 0) return +ORDER_DIRECTION;
				Debug.Assert(xLen == yLen, "Fitnesses must be compatible.");

				// In non-debug, all for the lesser scored to be of lesser importance.
				if (xLen < yLen) return -ORDER_DIRECTION;
				if (xLen > yLen) return +ORDER_DIRECTION;

				for (var i = 0; i < xLen; i++)
				{
					var sx = x.GetResult(i);
					var sy = y.GetResult(i);

					var c = sx.CompareTo(sy);
					if (c != 0) return c * ORDER_DIRECTION;
				}
			}

			return 0;
		}

		public static int SampleComparison(IFitness x, IFitness y)
			=> x.SampleCount.CompareTo(y.SampleCount) * ORDER_DIRECTION;

		public static int IdComparison(IFitness x, IFitness y)
		{
			if (x == y) return 0;
			if (x.ID < y.ID) return +ORDER_DIRECTION;
			if (x.ID > y.ID) return -ORDER_DIRECTION;
			return 0;
		}

		AsyncLock _lock;
		public AsyncLock Lock
		{
			get
			{
				return LazyInitializer.EnsureInitialized(ref _lock);
			}
		}

	}

	public static partial class FitnessExtensions
	{
		public static bool HasConverged(this IFitness fitness, uint minSamples = 100, double convergence = 1, double tolerance = 0)
		{
			if (minSamples > fitness.SampleCount) return false;
			foreach (var s in fitness.Scores)
			{
				if (s > convergence + double.Epsilon)
					throw new Exception("Score has exceeded convergence value: " + s);
				if (s.IsNearEqual(convergence, 0.0000001) && s.ToString() == convergence.ToString())
					continue;
				if (s < convergence - tolerance)
					return false;
			}
			return true;
		}

		public static bool IsSuperiorTo(this IFitness x, IFitness y)
			=> LegacyFitness.Comparison(x, y) == LegacyFitness.ORDER_DIRECTION;

		public static FitnessScore SnapShot(this IFitness fitness)
			=> fitness is FitnessScore f ? f : new FitnessScore(fitness);

		public static LegacyFitness Merge(this IEnumerable<IFitness> fitnesses)
			=> fitnesses.Aggregate(new LegacyFitness(), (prev, current) => prev.Merge(current));
	}
}
