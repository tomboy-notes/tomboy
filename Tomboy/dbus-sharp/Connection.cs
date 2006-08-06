namespace DBus 
{
  
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  using System.Reflection;
  using System.IO;
  using System.Collections;
  
  public delegate int DBusHandleMessageFunction (IntPtr rawConnection,
						 IntPtr rawMessage,
						 IntPtr userData);

  internal delegate void DBusObjectPathUnregisterFunction(IntPtr rawConnection,
							  IntPtr userData);

  internal delegate int DBusObjectPathMessageFunction(IntPtr rawConnection,
						      IntPtr rawMessage,
						      IntPtr userData);

  [StructLayout (LayoutKind.Sequential)]
  internal struct DBusObjectPathVTable
  {
    public DBusObjectPathUnregisterFunction unregisterFunction;
    public DBusObjectPathMessageFunction messageFunction;
    public IntPtr padding1;
    public IntPtr padding2;
    public IntPtr padding3;
    public IntPtr padding4;
    
    public DBusObjectPathVTable(DBusObjectPathUnregisterFunction unregisterFunction,
				DBusObjectPathMessageFunction messageFunction) 
    {
      this.unregisterFunction = unregisterFunction;
      this.messageFunction = messageFunction;
      this.padding1 = IntPtr.Zero;
      this.padding2 = IntPtr.Zero;
      this.padding3 = IntPtr.Zero;
      this.padding4 = IntPtr.Zero;
    }
  }

  public class Connection : IDisposable
  {
    /// <summary>
    /// A pointer to the underlying Connection structure
    /// </summary>
    private IntPtr rawConnection;
    
    /// <summary>
    /// The current slot number
    /// </summary>
    private static int slot = -1;
    
    private int timeout = -1;

    private ArrayList filters = new ArrayList ();      // of DBusHandleMessageFunction
    private ArrayList matches = new ArrayList ();      // of string
    private Hashtable object_paths = new Hashtable (); // key: string  value: DBusObjectPathVTable

    internal Connection(IntPtr rawConnection)
    {
      RawConnection = rawConnection;
    }
    
    public Connection(string address)
    {
      // the assignment bumps the refcount
      Error error = new Error();
      error.Init();
      RawConnection = dbus_connection_open(address, ref error);
      if (RawConnection != IntPtr.Zero) {
	dbus_connection_unref(RawConnection);
      } else {
	throw new DBusException(error);
      }

      SetupWithMain();
    }

    public void Dispose() 
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
    
    public void Dispose (bool disposing) 
    {
      if (disposing && RawConnection != IntPtr.Zero) 
	{
	  dbus_connection_disconnect(rawConnection);

	  RawConnection = IntPtr.Zero; // free the native object
	}
    }

    public void Flush()
    {
      dbus_connection_flush(RawConnection);
    }

    public void SetupWithMain() 
    {      
      dbus_connection_setup_with_g_main(RawConnection, IntPtr.Zero);
    }
    
    ~Connection () 
    {
      Dispose (false);
    }
    
    internal static Connection Wrap(IntPtr rawConnection) 
    {
      if (slot > -1) {
	// Maybe we already have a Connection object associated with
	// this rawConnection then return it
	IntPtr rawThis = dbus_connection_get_data (rawConnection, slot);
	if (rawThis != IntPtr.Zero) {
	  return (DBus.Connection) ((GCHandle)rawThis).Target;
	}
      }
      
      // If it doesn't exist then create a new connection around it
      return new Connection(rawConnection);
    }

    public void AddFilter (DBusHandleMessageFunction func)
    {
      if (!dbus_connection_add_filter (RawConnection,
				       func,
				       IntPtr.Zero,
				       IntPtr.Zero))
        throw new OutOfMemoryException ();

      this.filters.Add (func);
    }

    public void RemoveFilter (DBusHandleMessageFunction func)
    {
      dbus_connection_remove_filter (RawConnection, func, IntPtr.Zero);

      this.filters.Remove (func);
    }

    public void AddMatch (string match_rule)
    {
      dbus_bus_add_match (RawConnection, match_rule, IntPtr.Zero);

      this.matches.Add (match_rule);
    }

    public void RemoveMatch (string match_rule)
    {
      dbus_bus_remove_match (RawConnection, match_rule, IntPtr.Zero);

      this.matches.Remove (match_rule);
    }

    internal void RegisterObjectPath (string path, DBusObjectPathVTable vtable)
    {
      if (!dbus_connection_register_object_path (RawConnection, path, ref vtable, IntPtr.Zero))
        throw new OutOfMemoryException ();
 
      this.object_paths[path] = vtable;
    }
 
    internal void UnregisterObjectPath (string path)
    {
      dbus_connection_unregister_object_path (RawConnection, path);
 
      this.object_paths.Remove (path);
    }


    public string UniqueName
    {
      get
	{
	  return Marshal.PtrToStringAnsi (dbus_bus_get_unique_name (RawConnection));
	}
    }

    public int Timeout
    {
      get
	{
	  return this.timeout;
	}
      set
	{
	  this.timeout = value;
	}
    }
    
    private int Slot
    {
      get 
	{
	  if (slot == -1) 
	    {
	      // We need to initialize the slot
	      if (!dbus_connection_allocate_data_slot (ref slot))
		throw new OutOfMemoryException ();
	      
	      Debug.Assert (slot >= 0);
	    }
	  
	  return slot;
	}
    }
    
    internal IntPtr RawConnection 
    {
      get 
	{
	  return rawConnection;
	}
      set 
	{
	  if (value == rawConnection)
	    return;
	  
	  if (rawConnection != IntPtr.Zero) 
	    {
              // Remove our callbacks from this connection
              foreach (DBusHandleMessageFunction func in this.filters)
                dbus_connection_remove_filter (rawConnection, func, IntPtr.Zero);

              foreach (string match_rule in this.matches)
                dbus_bus_remove_match (rawConnection, match_rule, IntPtr.Zero);

              foreach (string path in this.object_paths.Keys)
                dbus_connection_unregister_object_path (rawConnection, path);

	      // Get the reference to this
	      IntPtr rawThis = dbus_connection_get_data (rawConnection, Slot);
	      Debug.Assert (rawThis != IntPtr.Zero);
	      
	      // Blank over the reference
	      dbus_connection_set_data (rawConnection, Slot, IntPtr.Zero, IntPtr.Zero);
	      
	      // Free the reference
	      ((GCHandle) rawThis).Free();
	      
	      // Unref the connection
	      dbus_connection_unref(rawConnection);
	    }
	  
	  this.rawConnection = value;
	  
	  if (rawConnection != IntPtr.Zero) 
	    {
	      GCHandle rawThis;
	      
	      dbus_connection_ref (rawConnection);
	      
	      // We store a weak reference to the C# object on the C object
	      rawThis = GCHandle.Alloc (this, GCHandleType.WeakTrackResurrection);
	      
	      dbus_connection_set_data(rawConnection, Slot, (IntPtr) rawThis, IntPtr.Zero);

              // Add the callbacks to this new connection
              foreach (DBusHandleMessageFunction func in this.filters)
                dbus_connection_add_filter (rawConnection, func, IntPtr.Zero, IntPtr.Zero);

              foreach (string match_rule in this.matches)
                dbus_bus_add_match (rawConnection, match_rule, IntPtr.Zero);

              foreach (string path in this.object_paths.Keys) {
                DBusObjectPathVTable vtable = (DBusObjectPathVTable) this.object_paths[path];
                dbus_connection_register_object_path (rawConnection, path, ref vtable, IntPtr.Zero);
	      }
	    }
	  else
	    {
	      this.filters.Clear ();
              this.matches.Clear ();
	      this.object_paths.Clear ();
	    }
	}
    }

    [DllImport("dbus-glib-1")]
    private extern static void dbus_connection_setup_with_g_main(IntPtr rawConnection,
							     IntPtr rawContext);
    
    [DllImport ("dbus-1")]
    private extern static IntPtr dbus_connection_open (string address, ref Error error);
    
    [DllImport ("dbus-1")]
    private extern static void dbus_connection_unref (IntPtr ptr);
    
    [DllImport ("dbus-1")]
    private extern static void dbus_connection_ref (IntPtr ptr);
    
    [DllImport ("dbus-1")]
    private extern static bool dbus_connection_allocate_data_slot (ref int slot);
    
    [DllImport ("dbus-1")]
    private extern static void dbus_connection_free_data_slot (ref int slot);
    
    [DllImport ("dbus-1")]
    private extern static bool dbus_connection_set_data (IntPtr ptr,
							 int    slot,
							 IntPtr data,
							 IntPtr free_data_func);
    
    [DllImport ("dbus-1")]
    private extern static void dbus_connection_flush (IntPtr  ptr);
    
    [DllImport ("dbus-1")]
    private extern static IntPtr dbus_connection_get_data (IntPtr ptr,
							   int    slot);
    
    [DllImport ("dbus-1")]
    private extern static void dbus_connection_disconnect (IntPtr ptr);

    [DllImport ("dbus-1")]
    private extern static IntPtr dbus_bus_get_unique_name (IntPtr ptr);

    [DllImport("dbus-1")]
    private extern static bool dbus_connection_add_filter(IntPtr rawConnection,
							  DBusHandleMessageFunction filter,
							  IntPtr userData,
							  IntPtr freeData);

    [DllImport("dbus-1")]
    private extern static void dbus_connection_remove_filter(IntPtr rawConnection,
							     DBusHandleMessageFunction filter,
							     IntPtr userData);

    [DllImport("dbus-1")]
    private extern static void dbus_bus_add_match(IntPtr rawConnection,
						  string rule,
						  IntPtr erro);

    [DllImport("dbus-1")]
    private extern static void dbus_bus_remove_match(IntPtr rawConnection,
						     string rule,
						     IntPtr erro);

    [DllImport ("dbus-1")]
    private extern static bool dbus_connection_register_object_path (IntPtr rawConnection,
								     string path,
								     ref DBusObjectPathVTable vTable,
								     IntPtr userData);

    [DllImport ("dbus-1")]
    private extern static void dbus_connection_unregister_object_path (IntPtr rawConnection,
								       string path);

  }
}
