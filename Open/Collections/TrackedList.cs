/*!
 * @author electricessence / https://github.com/electricessence/
 * Licensing: MIT https://github.com/electricessence/Genetic-Algorithm-Platform/blob/master/LICENSE.md
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Open.Threading;

namespace Open.Collections
{
	public class TrackedList<T> : ModificationSynchronizedBase, IList<T>
	{
		protected List<T> _source = new List<T>();

		public TrackedList(ModificationSynchronizer sync = null) : base(sync)
		{
		}


		public TrackedList(out ModificationSynchronizer sync) : base(out sync)
		{
		}

		protected override ModificationSynchronizer InitSync(object sync = null)
		{
			_syncOwned = true;
			return new ReadWriteModificationSynchronizer(sync as ReaderWriterLockSlim);
		}

		protected override void OnDispose(bool calledExplicitly)
		{
			base.OnDispose(calledExplicitly);
			var s = Interlocked.Exchange(ref _source, null);
			if (s != null)
			{
				s.Clear();
			}
		}

		public T this[int index]
		{
			get
			{
				return Sync.Reading(() =>
				{
					AssertIsLiving();
					return _source[index];
				});
			}

			set
			{
				SetValue(index, value);
			}
		}

		public bool SetValue(int index, T value)
		{
			return Sync.Modifying(AssertIsLiving, () => SetValueInternal(index, value));
		}

		private bool SetValueInternal(int index, T value)
		{
			var changing = index >= _source.Count || !_source[index].Equals(value);
			if (changing)
				_source[index] = value;
			return changing;
		}

		public int Count
		{
			get
			{
				return Sync.Reading(() =>
				{
					AssertIsLiving();
					return _source.Count;
				});
			}
		}

		public virtual void Add(T item)
		{
			Sync.Modifying(AssertIsLiving, () =>
			{
				_source.Add(item);
				return true;
			});
		}

		public void Add(T item, T item2, params T[] items)
		{
			AddThese(new T[] { item, item2 }.Concat(items));
		}

		public void AddThese(IEnumerable<T> items)
		{
			if (items != null && items.HasAny())
			{
				Sync.Modifying(AssertIsLiving, () =>
				{
					foreach (var item in items)
						Add(item); // Yes, we could just _source.Add() but this allows for overrideing Add();
					return true;
				});
			}

		}

		public void Clear()
		{
			Sync.Modifying(
				() => AssertIsLiving() && _source.Count != 0,
				() =>
				{
					var count = Count;
					bool hasItems = count != 0;
					if (hasItems)
					{
						_source.Clear();
					}
					return hasItems;
				});
		}

		public bool Contains(T item)
		{
			return Sync.Reading(() =>
				AssertIsLiving() && _source.Contains(item));
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			Sync.Reading(() =>
			{
				AssertIsLiving();
				_source.CopyTo(array, arrayIndex);
			});
		}

		public IEnumerator<T> GetEnumerator()
		{
			return Sync.Reading(() =>
			{
				AssertIsLiving();
				return _source.GetEnumerator();
			});
		}

		public int IndexOf(T item)
		{
			return Sync.Reading(() => AssertIsLiving() ? _source.IndexOf(item) : -1);
		}

		public void Insert(int index, T item)
		{
			Sync.Modifying(
				AssertIsLiving,
				() =>
				{
					_source.Insert(index, item);
					return true;
				});
		}

		public bool Remove(T item)
		{
			return Sync.Modifying(
				AssertIsLiving,
				() => _source.Remove(item));
		}

		public void RemoveAt(int index)
		{
			Sync.Modifying(
				AssertIsLiving,
				() =>
				{
					_source.RemoveAt(index);
					return true;
				});
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Sync.Reading(() =>
			{
				AssertIsLiving();
				return _source.GetEnumerator();
			});
		}


		public bool Replace(T target, T replacement, bool throwIfNotFound = false)
		{
			AssertIsLiving();
			int index = -1;
			return !target.Equals(replacement) && Sync.Modifying(
				() =>
				{
					AssertIsLiving();
					index = _source.IndexOf(target);
					if (index == -1)
					{
						if (throwIfNotFound)
							throw new ArgumentException("Not found.", "target");
						return false;
					}
					return true;
				},
				() =>
					SetValueInternal(index, replacement)
			);
		}

	}
}