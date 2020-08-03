using System;
using System.Collections.Generic;
using System.Text;

namespace ProfitTaking
{
	public interface ProfitTakingParameters
	{
		double MaxCapitalLossPercent { get; }

		double TargetRemainingShares { get; }

		double PriceRiseBeforeSell { get; }

	}
}
