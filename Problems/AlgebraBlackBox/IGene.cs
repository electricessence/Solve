using System;
using EvaluationFramework;
using System.Collections.Generic;

namespace BlackBoxFunction
{
    public interface IGene : IEvaluate<IReadOnlyList<double>,double>, IComparable<IGene>, ICloneable<IGene>
    {
        new IGene Clone();
    }

    public interface IGeneNode : IGene, IParent<IGene>, IEnumerable<IGene>
	{
		new IGeneNode Clone();
	}

}