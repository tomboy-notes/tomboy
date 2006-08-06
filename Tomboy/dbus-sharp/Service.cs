namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  using System.Collections;
  using System.Threading;
  using System.Reflection;
  using System.Reflection.Emit;
  
  public class Service
  {
    private Connection connection;
    private string name;
    private bool local = false;
    private Hashtable registeredHandlers = new Hashtable();
    private DBusHandleMessageFunction filterCalled;
    public delegate void SignalCalledHandler(Signal signal);
    public event SignalCalledHandler SignalCalled;
    private static AssemblyBuilder proxyAssembly;
    private ModuleBuilder module = null;

    // Add a match for signals. FIXME: Can we filter the service?
    private const string MatchRule = "type='signal'";

    internal Service(string name, Connection connection)
    {
      this.name = name;
      this.connection = connection;
      AddFilter();
    }

    public Service(Connection connection, string name)
    {
      Error error = new Error();
      error.Init();
      
      // This isn't used for now
      uint flags = 0;

      if (dbus_bus_request_name (connection.RawConnection, name, flags, ref error) == -1) {
	throw new DBusException(error);
      }

      this.connection = connection;
      this.name = name;
      this.local = true;
    }

    public static bool HasOwner(Connection connection, string name)
    {
      Error error = new Error();
      error.Init();
      
      if (dbus_bus_name_has_owner(connection.RawConnection, 
				  name, 
				  ref error)) {
	return true;
      } else {
	if (error.IsSet) {
	  throw new DBusException(error);
	}
	return false;
      }
    }

    public static Service Get(Connection connection, string name)
    {
      if (HasOwner(connection, name)) {
	return new Service(name, connection);
      } else {
	throw new ApplicationException("Name '" + name + "' does not exist.");
      }
    }

    public void UnregisterObject(object handledObject) 
    {
      registeredHandlers.Remove(handledObject);
    }

    public void RegisterObject(object handledObject, 
			       string pathName) 
    {
      Handler handler = new Handler(handledObject, pathName, this);
      registeredHandlers.Add(handledObject, handler);
    }

    internal Handler GetHandler(object handledObject) 
    {
      if (!registeredHandlers.Contains(handledObject)) {
	throw new ArgumentException("No handler registered for object: " + handledObject);
      }
      
      return (Handler) registeredHandlers[handledObject];
    }

    public object GetObject(Type type, string pathName)
    {
      ProxyBuilder builder = new ProxyBuilder(this, type, pathName);
      object proxy = builder.GetProxy();
      return proxy;
    }

    private void AddFilter() 
    {
      // Setup the filter function
      this.filterCalled = new DBusHandleMessageFunction(Service_FilterCalled);
      Connection.AddFilter (this.filterCalled);
      // Add a match for signals. FIXME: Can we filter the service?
      Connection.AddMatch ("type='signal'");
    }

    private int Service_FilterCalled(IntPtr rawConnection,
				    IntPtr rawMessage,
				    IntPtr userData) 
    {
      Message message = Message.Wrap(rawMessage, this);
      
      if (message.Type == Message.MessageType.Signal) {
	// We're only interested in signals
	Signal signal = (Signal) message;
	if (SignalCalled != null) {
	  Message.Push (message);
	  SignalCalled(signal);
	  Message.Pop ();
	}
      }
      
      message.Dispose ();

      return (int) Result.NotYetHandled;
    }

    public string Name
    {
      get
	{
	  return this.name;
	}
    }

    public Connection Connection 
    {
      get
	{
	  return connection;
	}
      
      set 
	{
	  this.connection = value;
	}
    }

    internal AssemblyBuilder ProxyAssembly
    {
      get {
	if (proxyAssembly == null){
	  AssemblyName assemblyName = new AssemblyName();
	  assemblyName.Name = "DBusProxy";
	  proxyAssembly = Thread.GetDomain().DefineDynamicAssembly(assemblyName, 
								   AssemblyBuilderAccess.RunAndSave);
	}
	
	return proxyAssembly;
      }
    }

    internal ModuleBuilder Module
    {
      get {
	if (this.module == null) {
	  this.module = ProxyAssembly.DefineDynamicModule(Name, Name + ".proxy.dll", true);
	}
	
	return this.module;
      }
    }

    [DllImport("dbus-1")]
    private extern static int dbus_bus_request_name(IntPtr rawConnection, 
						    string serviceName, 
						    uint flags, ref Error error);

    [DllImport("dbus-1")]
    private extern static bool dbus_bus_name_has_owner(IntPtr rawConnection, 
						       string serviceName, 
						       ref Error error);    

  }
}
