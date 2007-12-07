
using System;

namespace Tomboy
{
	/// <summary>
	/// Implement this interface if your Addin needs to do things when Tomboy
	/// starts up and shuts down.
	/// </summary>
	public abstract class ApplicationAddin : AbstractAddin
	{
		/// <summary>
		/// Called when Tomboy has started up and is nearly 100% initialized.
		/// </summary>
		public abstract void Initialize ();

		/// <summary>
		/// Called just before Tomboy shuts down for good.
		/// </summary>
		public abstract void Shutdown ();

		/// <summary>
		/// Return true if the addin is initialized
		/// </summary>
		public abstract bool Initialized
		{
			get;
		}
	}
}
