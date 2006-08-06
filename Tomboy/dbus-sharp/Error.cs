namespace DBus 
{
  
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  // FIXME add code to verify that size of DBus.Error
  // matches the C code.
  
  [StructLayout (LayoutKind.Sequential)]
  internal struct Error
  {
    internal IntPtr name;
    internal IntPtr message;
    private int dummies;
    private IntPtr padding1;
    
    public void Init() 
    {
      dbus_error_init(ref this);
    }
    
    public void Free() 
    {
      dbus_error_free(ref this);
    }
    
    public string Message
    {
      get
	{
	  return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(message);
	}
    }
    
    public string Name
    {
      get
	{
	  return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(name);
	}
    }

    public bool IsSet
    {
      get
	{
	  return (name != IntPtr.Zero);
	}
    }
    
    
    [DllImport ("dbus-1", EntryPoint="dbus_error_init")]
    private extern static void dbus_error_init (ref Error error);
    
    [DllImport ("dbus-1", EntryPoint="dbus_error_free")]
    private extern static void dbus_error_free (ref Error error);
  }
}
