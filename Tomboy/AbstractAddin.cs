
using System;

namespace Tomboy
{
	public abstract class AbstractAddin : IDisposable
	{
		bool disposing = false;

		~AbstractAddin ()
		{
			Dispose (false);
		}

		public void Dispose ()
		{
			disposing = true;
			Dispose (true);

			GC.SuppressFinalize (this);
		}

		protected virtual void Dispose (bool disposing)
		{
		}

		public bool IsDisposing
		{
			get {
				return disposing;
			}
		}
	}
}
