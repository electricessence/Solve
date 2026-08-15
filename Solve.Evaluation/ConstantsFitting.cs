using Open.Evaluation.Arithmetic;
using Open.Evaluation.Catalogs;
using Open.Evaluation.Core;
using Open.Hierarchy;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Solve.Evaluation;

/// <summary>
/// 25-0026: constants-fitting pass for numeric (<see cref="EvalGenome{T}"/> of <see cref="double"/>)
/// candidates. Competitive symbolic-regression systems separate structure search (evolution) from
/// local numeric optimization of a candidate's constant nodes; this is a dependency-free, bounded
/// Levenberg-Marquardt routine (Gauss-Newton with a Marquardt damping term) using central-difference
/// numeric differentiation, since no external numeric packages are permitted (see the task's scope
/// boundary).
/// </summary>
/// <remarks>
/// How constants are discovered/rebuilt (verified against the real Open.Evaluation 1.1.2 package
/// via a throwaway reflection+compile probe -- the newer restructured Open.Evaluation checkout under
/// Open-NET-Libraries is NOT what this repo consumes and was orientation-only):
/// <list type="bullet">
/// <item>An <see cref="EvaluationCatalog{T}"/>'s <c>Factory.Map(root)</c> wraps an existing
/// <see cref="IEvaluate{T}"/> tree in a mutable <see cref="Node{T}"/> hierarchy without cloning the
/// underlying evaluation nodes; <c>Node&lt;IEvaluate&lt;double&gt;&gt;.GetDescendantsOfType()</c>
/// (an <c>Open.Hierarchy</c> traversal, despite the name it is generic over the *node* type, not a
/// value-type filter) enumerates every descendant node in a stable, deterministic (pre-order) order
/// for a given unmutated root.</item>
/// <item>Constant nodes implement <see cref="IConstant{TResult}"/> (concretely
/// <c>Open.Evaluation.Core.Constant&lt;double&gt;</c>); a node's fitted value is applied by writing
/// a new <c>Constant&lt;double&gt;</c> to <see cref="Node{T}.Value"/> and then calling
/// <c>EvaluationCatalog&lt;double&gt;.FixHierarchy(tree).Recycle()</c>, which reconciles the parent
/// chain and returns the rebuilt <see cref="IEvaluate{T}"/> root. This path was verified NOT to
/// re-trigger the catalog's "smart construction" identity-stripping (e.g. <c>Registry.Arithmetic
/// .GetOperator</c> silently canonicalizes a literal <c>x * 1</c> down to just <c>x</c> at
/// construction time) -- <c>FixHierarchy</c> preserves whatever constant value is written, including
/// exactly 1 or 0, so the optimizer's trial values are never silently dropped mid-fit.</item>
/// <item><c>Exponent&lt;double&gt;</c> stores its exponent (e.g. 2 for square, -1 for the
/// reciprocal, 0.5 for square root) as a genuine <see cref="IConstant{TResult}"/> child reachable
/// through the same traversal. Fitting that value would let the optimizer freely drift "square" into
/// an arbitrary, continuously-tunable power -- a structural change entangled with (and destabilizing
/// for) domain behavior (e.g. a negative base raised to a drifting fractional power produces NaN),
/// not the "tune the coefficients, keep the shape" contract this pass is meant to have. So exponent
/// power operands are deliberately excluded from the fittable set; only genuine coefficient/offset
/// constants (Sum/Product operands, catalog-added constants, etc.) are fit. Expression simplification
/// and structural search remain a separate concern per the task's scope boundary.</item>
/// </list>
/// </remarks>
public static class ConstantsFitting
{
	/// <summary>Default bound on optimizer outer iterations.</summary>
	public const int DefaultMaxIterations = 50;

	/// <summary>Default wall-clock bound for a single <see cref="Fit"/> call.</summary>
	public static readonly TimeSpan DefaultTimeBudget = TimeSpan.FromMilliseconds(200);

	// Relative step used for central-difference numeric differentiation.
	const double DiffStep = 1e-6;

	// Levenberg-Marquardt damping schedule.
	const double InitialLambda = 1e-3;
	const double LambdaDecreaseFactor = 10;
	const double LambdaIncreaseFactor = 10;
	const double LambdaMax = 1e12;
	const double LambdaMin = 1e-12;
	const int MaxLambdaAttemptsPerIteration = 12;

	// Marquardt-style scaled diagonal damping; the additive epsilon keeps the system well-posed
	// even when a JtJ diagonal entry is exactly zero (e.g. a constant with zero local sensitivity).
	const double DiagonalEpsilon = 1e-12;

	// Below this, a pivot is treated as numerically singular and the elimination bails out.
	const double PivotEpsilon = 1e-14;

	/// <summary>
	/// Fits <paramref name="genome"/>'s constant nodes against <paramref name="samples"/> using a
	/// bounded, dependency-free Levenberg-Marquardt routine. Never throws: singular, non-converging,
	/// or domain-invalid (NaN/Infinity-producing) cases fall back to the best-so-far constants, or to
	/// <paramref name="genome"/> itself, unchanged, when no improving fit is found.
	/// </summary>
	/// <param name="catalog">The genome's owning catalog (e.g. a factory's <c>Catalog</c>), used to
	/// rebuild the tree with fitted constant values.</param>
	/// <param name="genome">The candidate to fit. Its structure is never altered -- only constant
	/// node values change.</param>
	/// <param name="samples">The sample set to fit against: each entry pairs a parameter vector with
	/// the target output.</param>
	/// <param name="maxIterations">Bound on optimizer outer iterations. Must be positive to have any
	/// effect; non-positive values are treated as "don't fit" and return <paramref name="genome"/>
	/// unchanged.</param>
	/// <param name="timeBudget">Wall-clock bound for the whole call. Defaults to
	/// <see cref="DefaultTimeBudget"/>.</param>
	/// <returns>
	/// A new <see cref="EvalGenome{T}"/> with identical structure and fitted constants, or
	/// <paramref name="genome"/> itself (same reference) when it has no fittable constant nodes, the
	/// starting point is already domain-invalid for this sample set, or no iteration produced an
	/// improving step.
	/// </returns>
	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "AC2 requires this routine to never throw regardless of what Open.Evaluation does internally (arbitrary candidate trees, arbitrary samples); every foreseeable failure is a signal to fall back, not to propagate.")]
	public static EvalGenome<double> Fit(
		EvaluationCatalog<double> catalog,
		EvalGenome<double> genome,
		IReadOnlyList<(IReadOnlyList<double> Input, double Target)> samples,
		int maxIterations = DefaultMaxIterations,
		TimeSpan? timeBudget = null)
	{
		ArgumentNullException.ThrowIfNull(catalog);
		ArgumentNullException.ThrowIfNull(genome);
		ArgumentNullException.ThrowIfNull(samples);

		if (samples.Count == 0 || maxIterations <= 0)
			return genome;

		try
		{
			return FitCore(catalog, genome, samples, maxIterations, timeBudget ?? DefaultTimeBudget);
		}
		catch
		{
			// Belt-and-braces: this routine must never throw (AC2). Every internal numeric operation
			// is already guarded, but any unforeseen failure (e.g. an Open.Evaluation edge case) falls
			// back to the original, untouched genome rather than propagating.
			return genome;
		}
	}

	static EvalGenome<double> FitCore(
		EvaluationCatalog<double> catalog,
		EvalGenome<double> genome,
		IReadOnlyList<(IReadOnlyList<double> Input, double Target)> samples,
		int maxIterations,
		TimeSpan timeBudget)
	{
		IEvaluate<double> root = genome.Root;

		Node<IEvaluate<double>> initialTree = catalog.Factory.Map(root);
		Node<IEvaluate<double>>[] initialNodes = GetFittableConstantNodes(initialTree);
		int n = initialNodes.Length;
		if (n == 0)
			return genome; // AC4: no constants to fit.

		double[] x0 = new double[n];
		for (int i = 0; i < n; i++)
			x0[i] = ((IConstant<double>)initialNodes[i].Value!).Value;

		var residuals = new double[samples.Count];
		double sse = ComputeSse(catalog, root, x0, samples, residuals);

		double[] bestX = x0; // Reference stays x0 until (and unless) a strictly-improving step is found.
		double bestSse = double.IsFinite(sse) ? sse : double.PositiveInfinity;
		if (!double.IsFinite(sse))
		{
			// The starting point is already domain-invalid for this sample set (e.g. a negative
			// input under a square-root subtree) -- no local perturbation fixes that, so there's
			// nothing safe to iterate from.
			return genome;
		}

		var sw = Stopwatch.StartNew();
		double[] x = (double[])x0.Clone();
		double[] r = residuals;
		double lambda = InitialLambda;

		for (int iteration = 0; iteration < maxIterations && sw.Elapsed < timeBudget; iteration++)
		{
			double[][]? jacobianColumns = ComputeJacobianColumns(catalog, root, x, samples, timeBudget, sw);
			if (jacobianColumns is null)
				break; // Domain error differentiating around x, or ran out of time.

			(double[][] jtj, double[] jtr) = BuildNormalEquations(jacobianColumns, r, n);

			bool accepted = false;
			for (int attempt = 0; attempt < MaxLambdaAttemptsPerIteration && sw.Elapsed < timeBudget; attempt++)
			{
				double[][] a = CloneMatrix(jtj);
				for (int d = 0; d < n; d++)
					a[d][d] += lambda * (jtj[d][d] + DiagonalEpsilon);

				var negJtr = new double[n];
				for (int d = 0; d < n; d++)
					negJtr[d] = -jtr[d];

				double[]? delta = SolveSymmetric(a, negJtr);
				if (delta is null)
				{
					lambda *= LambdaIncreaseFactor;
					if (lambda > LambdaMax) break;
					continue;
				}

				var xTrial = new double[n];
				for (int d = 0; d < n; d++)
					xTrial[d] = x[d] + delta[d];

				var rTrial = new double[samples.Count];
				double sseTrial = ComputeSse(catalog, root, xTrial, samples, rTrial);

				if (double.IsFinite(sseTrial) && sseTrial < sse)
				{
					x = xTrial;
					sse = sseTrial;
					r = rTrial;
					lambda = Math.Max(lambda / LambdaDecreaseFactor, LambdaMin);
					accepted = true;
					break;
				}

				lambda *= LambdaIncreaseFactor;
				if (lambda > LambdaMax) break;
			}

			if (sse < bestSse)
			{
				bestSse = sse;
				bestX = (double[])x.Clone();
			}

			if (!accepted)
				break; // Converged, stalled, or singular past recovery -- stop iterating (AC2).
		}

		// No improving step was ever accepted: bestX is still literally x0 (never reassigned above),
		// so keep the caller's original genome instance rather than rebuilding an equivalent tree.
		return ReferenceEquals(bestX, x0)
			? genome
			: new EvalGenome<double>(Rebuild(catalog, root, bestX));
	}

	/// <summary>
	/// Central-difference Jacobian of the residual vector with respect to each fittable constant, one
	/// column (length = sample count) per constant. Returns <see langword="null"/> if a domain error
	/// is hit while differentiating, or the time budget is exhausted mid-computation.
	/// </summary>
	static double[][]? ComputeJacobianColumns(
		EvaluationCatalog<double> catalog,
		IEvaluate<double> root,
		double[] x,
		IReadOnlyList<(IReadOnlyList<double> Input, double Target)> samples,
		TimeSpan timeBudget,
		Stopwatch sw)
	{
		int n = x.Length;
		int m = samples.Count;
		var columns = new double[n][];
		var plusResiduals = new double[m];
		var minusResiduals = new double[m];

		for (int j = 0; j < n; j++)
		{
			if (sw.Elapsed >= timeBudget)
				return null;

			double step = Math.Max(DiffStep, Math.Abs(x[j]) * DiffStep);

			var xPlus = (double[])x.Clone();
			xPlus[j] += step;
			var xMinus = (double[])x.Clone();
			xMinus[j] -= step;

			double ssePlus = ComputeSse(catalog, root, xPlus, samples, plusResiduals);
			double sseMinus = ComputeSse(catalog, root, xMinus, samples, minusResiduals);
			if (!double.IsFinite(ssePlus) || !double.IsFinite(sseMinus))
				return null;

			double denom = 2 * step;
			var column = new double[m];
			for (int i = 0; i < m; i++)
				column[i] = (plusResiduals[i] - minusResiduals[i]) / denom;
			columns[j] = column;
		}

		return columns;
	}

	static (double[][] JtJ, double[] Jtr) BuildNormalEquations(double[][] jacobianColumns, double[] residuals, int n)
	{
		int m = residuals.Length;
		var jtj = new double[n][];
		for (int a = 0; a < n; a++) jtj[a] = new double[n];
		var jtr = new double[n];

		for (int a = 0; a < n; a++)
		{
			double[] colA = jacobianColumns[a];
			for (int b = a; b < n; b++)
			{
				double[] colB = jacobianColumns[b];
				double sum = 0;
				for (int i = 0; i < m; i++)
					sum += colA[i] * colB[i];
				jtj[a][b] = sum;
				jtj[b][a] = sum;
			}

			double sumR = 0;
			for (int i = 0; i < m; i++)
				sumR += colA[i] * residuals[i];
			jtr[a] = sumR;
		}

		return (jtj, jtr);
	}

	/// <summary>
	/// Solves the small dense n x n linear system via Gaussian elimination with partial pivoting.
	/// Returns <see langword="null"/> when a pivot is numerically singular (below
	/// <see cref="PivotEpsilon"/>) even after Marquardt damping, so callers can back off (increase
	/// damping) rather than propagate garbage.
	/// </summary>
	static double[]? SolveSymmetric(double[][] a, double[] b)
	{
		int n = b.Length;
		double[][] m = CloneMatrix(a);
		var x = (double[])b.Clone();

		for (int col = 0; col < n; col++)
		{
			int pivot = col;
			double best = Math.Abs(m[col][col]);
			for (int row = col + 1; row < n; row++)
			{
				double v = Math.Abs(m[row][col]);
				if (v > best) { best = v; pivot = row; }
			}

			if (best < PivotEpsilon)
				return null;

			if (pivot != col)
			{
				(m[col], m[pivot]) = (m[pivot], m[col]);
				(x[col], x[pivot]) = (x[pivot], x[col]);
			}

			double[] pivotRow = m[col];
			for (int row = col + 1; row < n; row++)
			{
				double[] currentRow = m[row];
				double factor = currentRow[col] / pivotRow[col];
				if (factor == 0) continue;
				for (int c = col; c < n; c++)
					currentRow[c] -= factor * pivotRow[c];
				x[row] -= factor * x[col];
			}
		}

		var result = new double[n];
		for (int i = n - 1; i >= 0; i--)
		{
			double sum = x[i];
			for (int j = i + 1; j < n; j++)
				sum -= m[i][j] * result[j];
			if (Math.Abs(m[i][i]) < PivotEpsilon)
				return null;
			result[i] = sum / m[i][i];
		}

		return result;
	}

	static double[][] CloneMatrix(double[][] source)
	{
		var clone = new double[source.Length][];
		for (int i = 0; i < source.Length; i++)
			clone[i] = (double[])source[i].Clone();
		return clone;
	}

	/// <summary>
	/// Evaluates the candidate built from <paramref name="constants"/> against every sample, writing
	/// per-sample residuals (predicted - target) into <paramref name="residuals"/> and returning the
	/// sum of squared residuals -- or <see cref="double.PositiveInfinity"/> if rebuilding/evaluating
	/// throws or produces a non-finite value anywhere (a domain error such as a negative
	/// square-root), so LM always treats that trial as strictly worse and backs off.
	/// </summary>
	[SuppressMessage("Design", "CA1031:Do not catch general exception types",
		Justification = "Arbitrary Open.Evaluation candidate trees evaluated against arbitrary samples; any failure here just means this trial is invalid, so it's scored as +Infinity (rejected) rather than propagated. See AC2.")]
	static double ComputeSse(
		EvaluationCatalog<double> catalog,
		IEvaluate<double> root,
		double[] constants,
		IReadOnlyList<(IReadOnlyList<double> Input, double Target)> samples,
		double[] residuals)
	{
		IEvaluate<double> candidate;
		try
		{
			candidate = Rebuild(catalog, root, constants);
		}
		catch
		{
			return double.PositiveInfinity;
		}

		double sse = 0;
		int count = samples.Count;
		for (int i = 0; i < count; i++)
		{
			double predicted;
			try
			{
				predicted = candidate.Evaluate(samples[i].Input);
			}
			catch
			{
				return double.PositiveInfinity;
			}

			double residual = predicted - samples[i].Target;
			if (!double.IsFinite(residual))
				return double.PositiveInfinity;

			residuals[i] = residual;
			sse += residual * residual;
		}

		return sse;
	}

	/// <summary>
	/// Rebuilds <paramref name="root"/> with its fittable constant nodes set to
	/// <paramref name="values"/> (in the same order <see cref="GetFittableConstantNodes"/> returns
	/// them for the same, unmutated root). <paramref name="root"/> itself is left untouched --
	/// <c>Factory.Map</c> wraps it without cloning the underlying evaluation nodes, and
	/// <c>FixHierarchy(...).Recycle()</c> produces an independent rebuilt tree.
	/// </summary>
	static IEvaluate<double> Rebuild(EvaluationCatalog<double> catalog, IEvaluate<double> root, double[] values)
	{
		Node<IEvaluate<double>> tree = catalog.Factory.Map(root);
		Node<IEvaluate<double>>[] nodes = GetFittableConstantNodes(tree);
		for (int i = 0; i < nodes.Length; i++)
			nodes[i].Value = catalog.GetConstant(values[i]);
		return catalog.FixHierarchy(tree).Recycle()!;
	}

	/// <summary>
	/// The constant nodes eligible for fitting: every <see cref="IConstant{TResult}"/> descendant
	/// except an <see cref="Exponent{T}"/>'s power operand (see the type-level remarks for why powers
	/// are excluded).
	/// </summary>
	static Node<IEvaluate<double>>[] GetFittableConstantNodes(Node<IEvaluate<double>> tree)
		=> tree.GetDescendantsOfType()
			.Where(n => n.Value is IConstant<double> && !IsExponentPower(n))
			.ToArray();

	static bool IsExponentPower(Node<IEvaluate<double>> node)
		=> node.Parent?.Value is Exponent<double> exponent && ReferenceEquals(exponent.Power, node.Value);
}
