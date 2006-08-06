namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  public class Bus
  {
    // Keep in sync with C
    private enum BusType 
    {
      Session = 0,
      System = 1,
      Activation = 2
    }

    // Don't allow instantiation
    private Bus () { }

    public static Connection GetSessionBus() 
    {
      return GetBus(BusType.Session);
    }

    public static Connection GetSystemBus()
    {
      return GetBus(BusType.System);
    }

    private static Connection GetBus(BusType busType) 
    {
      Error error = new Error();
      error.Init();
      
      IntPtr rawConnection = dbus_bus_get((int) busType, ref error);
      
      if (rawConnection != IntPtr.Zero) {
	Connection connection = Connection.Wrap(rawConnection);
	connection.SetupWithMain();
	dbus_connection_unref(rawConnection);

	return connection;
      } else {
	throw new DBusException(error);
      }
    }

    [DllImport ("dbus-1")]
    private extern static IntPtr dbus_bus_get (int which, ref Error error);

    [DllImport ("dbus-1")]
    private extern static void dbus_connection_unref (IntPtr ptr);
  }
}
