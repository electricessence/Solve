﻿/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Open/blob/dotnet-core/LICENSE.md
 */

using System.Threading;


namespace System
{
    public static class Lazy
	{
		public static Lazy<T> New<T>(Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication)
		{
			if(factory==null)
				throw new ArgumentNullException("factory");

			return new Lazy<T>(factory, mode);
		}

		public static Lazy<T> NewLazy<T>(this Func<T> factory, LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication)
		{
			if(factory==null)
				throw new ArgumentNullException("factory");

			return new Lazy<T>(factory, mode);
		}
	}
}