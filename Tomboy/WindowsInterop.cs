//#if WIN32

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Tomboy.Windows.Interop
{
	public enum eKnownDestCategory
	{
		Frequent = 1,
		Recent
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct WIN32_FIND_DATAW
	{
		public int FileAttributes;
		public FILETIME CreationTime;
		public FILETIME LastAccessTime;
		public FILETIME LastWriteTime;
		public int FileSizeHigh;
		public int FileSizeLow;
		public int Reserved0;
		public int Reserved1;
		[MarshalAs (UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
		public string FileName;
		[MarshalAs (UnmanagedType.ByValTStr, SizeConst = 14)]
		public string AlternateFileName;
		private const int MAX_PATH = 260;
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct PROPERTYKEY
	{
		public Guid fmtid;
		public uint pid;
	}

	[StructLayout (LayoutKind.Sequential)]
	public struct PROPVARIANT
	{
		ushort vt;
		ushort wReserved1;
		ushort wReserved2;
		ushort wReserved3;
		IntPtr p;
		int p2;

		private byte [] GetDataBytes ()
		{
			byte [] ret = new byte [IntPtr.Size + sizeof (int)];
			if (IntPtr.Size == 4)
				BitConverter.GetBytes (p.ToInt32 ()).CopyTo (ret, 0);
			else if (IntPtr.Size == 8)
				BitConverter.GetBytes (p.ToInt64 ()).CopyTo (ret, 0);
			BitConverter.GetBytes (p2).CopyTo (ret, IntPtr.Size);
			return ret;
		}

		[DllImport ("ole32.dll")]
		private extern static int PropVariantClear (ref PROPVARIANT pvar);

		public void Clear ()
		{
			PROPVARIANT var = this;
			PropVariantClear (ref var);

			vt = (ushort) VarEnum.VT_EMPTY;
			wReserved1 = wReserved2 = wReserved3 = 0;
			p = IntPtr.Zero;
			p2 = 0;
		}

		public VarEnum Type
		{
			get { return (VarEnum) vt; }
		}

		public object Value
		{
			get
			{
				switch ((VarEnum) vt) {
					case VarEnum.VT_LPWSTR:
						return Marshal.PtrToStringUni (p);
					case VarEnum.VT_UNKNOWN:
						return Marshal.GetObjectForIUnknown (p);
					case VarEnum.VT_DISPATCH:
						return p;
					default:
						throw new NotSupportedException ("The type of this variable is not support ('" + vt.ToString () + "')");
				}
			}
		}

		public void SetString (string value)
		{
			vt = (ushort) VarEnum.VT_LPWSTR;
			p = Marshal.StringToCoTaskMemUni (value);
		}
	}

	[ComImport, Guid ("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellItem
	{
		void BindToHandler ([In] IBindCtx bindCtx, [In] ref Guid bhid, [In] ref Guid riid, [Out] out object ppv);

		void GetParent ([Out] IShellItem shellItem);

		void GetDisplayName ([In] uint sigdnName, [Out, MarshalAs (UnmanagedType.LPWStr)] out string name);

		void GetAttributes ([In] uint mask, [Out] out uint attributes);

		void Compare ([In] IShellItem shellItem, [In] uint sichIntf, [Out] out int order);
	}

	[ComImport, Guid ("000214F9-0000-0000-C000-000000000046")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	public interface IShellLinkW
	{
		void GetPath ([Out, MarshalAs (UnmanagedType.LPWStr)] out string file, [In] int cch, [In, Out] WIN32_FIND_DATAW data, [In] uint flags);

		void GetIDList ([Out] IntPtr idl);

		void SetIDList ([In] IntPtr idl);

		void GetDescription ([Out, MarshalAs (UnmanagedType.LPWStr)] out string name, [In] int cch);

		void SetDescription ([In, MarshalAs (UnmanagedType.LPWStr)] string name);

		void GetWorkingDirectory ([Out, MarshalAs (UnmanagedType.LPWStr)] out string name, [In] int cch);

		void SetWorkingDirectory ([In, MarshalAs (UnmanagedType.LPWStr)] string name);

		void GetArguments ([Out, MarshalAs (UnmanagedType.LPWStr)] out string name, [In] int cch);

		void SetArguments ([In, MarshalAs (UnmanagedType.LPWStr)] string name);

		void GetHotkey ([Out] out ushort hotkey);

		void SetHotkey ([In] ushort hotkey);

		void GetShowCmd ([Out] out int showCmd);

		void SetShowCmd ([In] int showCmd);

		void GetIconLocation ([Out, MarshalAs (UnmanagedType.LPWStr)] out string iconPath, [In] int cch, [Out] int icon);

		void SetIconLocation ([In, MarshalAs (UnmanagedType.LPWStr)] string iconPath, [In] int icon);

		void SetRelativePath ([In, MarshalAs (UnmanagedType.LPWStr)] string pathrel, [In] ushort reserved);

		void Resolve ([In] IntPtr hwnd, [In] ushort flags);

		void SetPath ([In, MarshalAs (UnmanagedType.LPWStr)] string path);
	}

	[ComImport, Guid ("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	public interface IPropertyStore
	{
		void GetCount ([Out] out ushort cProps);

		void GetAt ([In] ushort prop, [Out] out PROPERTYKEY key);

		void GetValue ([In] ref PROPERTYKEY key, [Out] out PROPVARIANT val);

		void SetValue ([In] ref PROPERTYKEY key, [In] ref PROPVARIANT val);

		void Commit ();
	}

	[ComImport, Guid ("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	public interface IObjectArray
	{
		void GetCount ([Out] out uint cObjects);

		void GetAt ([In] uint uiIndex, [In] ref Guid riid, [Out, MarshalAs (UnmanagedType.Interface)] out object ppv);
	}

	[ComImport, Guid ("5632b1a4-e38a-400a-928a-d4cd63230295")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	public interface IObjectCollection
	{
		[PreserveSig]
		void GetCount ([Out] out uint cObjects);

		[PreserveSig]
		void GetAt ([In] uint uiIndex, [In] ref Guid riid, [Out, MarshalAs (UnmanagedType.Interface)] out object ppv);

		void AddObject ([In, MarshalAs (UnmanagedType.Interface)] object pvObject);

		void AddFromArray ([In, MarshalAs (UnmanagedType.Interface)] IObjectArray source);

		void RemoveObjectAt ([In] uint index);

		void Clear ();
	}

	[ComImport, Guid ("6332debf-87b5-4670-90c0-5e57b408a49e")]
	[InterfaceType (ComInterfaceType.InterfaceIsIUnknown)]
	public interface ICustomDestinationList
	{
		void SetAppID ([In, MarshalAs (UnmanagedType.LPWStr)] string appID);

		void BeginList ([Out] out uint cMinSlots, [In] ref Guid riid, [Out, MarshalAs (UnmanagedType.Interface)] out IObjectArray ppv);

		void AppendCategory ([In, MarshalAs (UnmanagedType.LPWStr)] string category, [In] IObjectArray objectArray);

		void AppendKnownCategory ([In] eKnownDestCategory category);

		void AddUserTasks ([In, MarshalAs (UnmanagedType.Interface)] IObjectArray objectArray);

		void CommitList ();

		void GetRemovedDestinations ([In] Guid riid, [Out] out object ppv);

		void DeleteList ([In, MarshalAs (UnmanagedType.LPWStr)] string appID);

		void AbortList ();
	}

	public struct CLSID
	{
		public static readonly Guid IObjectArray = new Guid ("92CA9DCD-5622-4bba-A805-5E9F541BD8C9");
		public static readonly Guid IPropertyStore = new Guid ("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
		public static readonly Guid IShellLinkW = new Guid ("000214F9-0000-0000-C000-000000000046");
		public static readonly Guid DestinationList = new Guid ("77f10cf0-3db5-4966-b520-b7c54fd35ed6");
		public static readonly Guid EnumerableObjectCollection = new Guid ("2d3468c1-36a7-43b6-ac24-d3f02fd9607a");
		public static readonly Guid ShellItem = new Guid ("9ac9fbe1-e0a2-4ad6-b4ee-e212013ea917");
		public static readonly Guid ShellLink = new Guid ("00021401-0000-0000-C000-000000000046");
	}
}

//#endif // WIN32
