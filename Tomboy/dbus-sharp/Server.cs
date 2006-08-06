namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  public class Server
  {
    private IntPtr rawServer;
    
    /// <summary>
    /// The current slot number
    /// </summary>
    private static int slot = -1;

    private string address = null;
    
    private Server(IntPtr rawServer)
    {
      RawServer = rawServer;
    }
    
    public Server(string address)
    {
      Error error = new Error();
      error.Init();
      RawServer = dbus_server_listen(address, ref error);
      if (RawServer != IntPtr.Zero){
	dbus_server_unref(RawServer);
      } else {
	throw new DBusException(error);
      }
    }
    
    ~Server()
    {
      if (RawServer != IntPtr.Zero) {
	dbus_server_unref(rawServer);
      }
      
      RawServer = IntPtr.Zero;
    }
    
    public string Address 
    {
      get
	{
	  if (address == null) {
	    address = dbus_server_get_address(rawServer);
	  }
	  
	  return address;
	}
    }

    private int Slot
    {
      get 
	{
	  if (slot == -1) 
	    {
	      // We need to initialize the slot
	      if (!dbus_server_allocate_data_slot (ref slot))
		throw new OutOfMemoryException ();
	      
	      Debug.Assert (slot >= 0);
	    }
	  
	  return slot;
	}
    }

    internal IntPtr RawServer 
    {
      get 
	{
	  return rawServer;
	}
      set 
	{
	  if (value == rawServer)
	    return;
	  
	  if (rawServer != IntPtr.Zero) 
	    {
	      // Get the reference to this
	      IntPtr rawThis = dbus_server_get_data (rawServer, Slot);
	      Debug.Assert (rawThis != IntPtr.Zero);
	      
	      // Blank over the reference
	      dbus_server_set_data (rawServer, Slot, IntPtr.Zero, IntPtr.Zero);
	      
	      // Free the reference
	      ((GCHandle) rawThis).Free();
	      
	      // Unref the connection
	      dbus_server_unref(rawServer);
	    }
	  
	  this.rawServer = value;
	  
	  if (rawServer != IntPtr.Zero) 
	    {
	      GCHandle rawThis;
	      
	      dbus_server_ref (rawServer);
	      
	      // We store a weak reference to the C# object on the C object
	      rawThis = GCHandle.Alloc (this, GCHandleType.WeakTrackResurrection);
	      
	      dbus_server_set_data(rawServer, Slot, (IntPtr) rawThis, IntPtr.Zero);
	    }
	}
    }

    [DllImport ("dbus-1", EntryPoint="dbus_server_listen")]
    private extern static IntPtr dbus_server_listen(string address, ref Error error);

    [DllImport ("dbus-1", EntryPoint="dbus_server_unref")]
    private extern static IntPtr dbus_server_unref(IntPtr rawServer);

    [DllImport ("dbus-1", EntryPoint="dbus_server_ref")]
    private extern static void dbus_server_ref(IntPtr rawServer);

    [DllImport ("dbus-1", EntryPoint="dbus_server_disconnect")]
    private extern static void dbus_server_disconnect(IntPtr rawServer);

    [DllImport ("dbus-1", EntryPoint="dbus_server_get_address")]
    private extern static string dbus_server_get_address(IntPtr rawServer);

    [DllImport ("dbus-1", EntryPoint="dbus_server_set_data")]
    private extern static bool dbus_server_set_data(IntPtr rawServer,
						    int slot,
						    IntPtr data,
						    IntPtr freeDataFunc);

    [DllImport ("dbus-1", EntryPoint="dbus_server_get_data")]
    private extern static IntPtr dbus_server_get_data(IntPtr rawServer,
						      int slot);

    [DllImport ("dbus-1", EntryPoint="dbus_server_allocate_data_slot")]
    private extern static bool dbus_server_allocate_data_slot (ref int slot);
    
    [DllImport ("dbus-1", EntryPoint="dbus_server_free_data_slot")]
    private extern static void dbus_server_free_data_slot (ref int slot);

  }
}
