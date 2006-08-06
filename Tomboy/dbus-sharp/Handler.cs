namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  using System.Reflection;
  using System.Collections;

  internal enum Result 
  {
    Handled = 0,
    NotYetHandled = 1,
    NeedMemory = 2
  }

  internal class Handler
  {
    private string path = null;
    private Introspector introspector = null;
    private object handledObject = null;
    private DBusObjectPathVTable vTable;
    private Connection connection;
    private Service service;

    // We need to hold extra references to these callbacks so that they don't
    // get garbage collected before they are called back into from unmanaged
    // code.
    private DBusObjectPathUnregisterFunction unregister_func;
    private DBusObjectPathMessageFunction message_func;

    public Handler(object handledObject, 
		   string path, 
		   Service service)
    {
      Service = service;
      Connection = service.Connection;
      HandledObject = handledObject;
      this.path = path;
      
      // Create the vTable and register the path
      this.unregister_func = new DBusObjectPathUnregisterFunction (Unregister_Called);
      this.message_func = new DBusObjectPathMessageFunction (Message_Called);

      vTable = new DBusObjectPathVTable (this.unregister_func, this.message_func);
      Connection.RegisterObjectPath (Path, vTable);
      RegisterSignalHandlers();
    }

    private void RegisterSignalHandlers()
    {
      ProxyBuilder proxyBuilder = new ProxyBuilder(Service, HandledObject.GetType(), Path);

      foreach (DictionaryEntry interfaceEntry in this.introspector.InterfaceProxies) {
	InterfaceProxy interfaceProxy = (InterfaceProxy) interfaceEntry.Value;
	foreach (DictionaryEntry signalEntry in interfaceProxy.Signals) {
	  EventInfo eventE = (EventInfo) signalEntry.Value;
	  Delegate del = Delegate.CreateDelegate(eventE.EventHandlerType, proxyBuilder.GetSignalProxy(), "Proxy_" + eventE.Name);
	  eventE.AddEventHandler(HandledObject, del);
	}
      }
    }

    public object HandledObject 
    {
      get {
	return this.handledObject;
      }
      
      set {
	this.handledObject = value;
	
	// Register the methods
	this.introspector = Introspector.GetIntrospector(value.GetType());	  
      }
    }

    public void Unregister_Called(IntPtr rawConnection, 
				  IntPtr userData)
    {
      if (service != null) {
	service.UnregisterObject(HandledObject);
      }

      path = null;
    }

    private int Message_Called(IntPtr rawConnection, 
			       IntPtr rawMessage, 
			       IntPtr userData) 
    {
      Message message = Message.Wrap(rawMessage, Service);
      Result res = Result.NotYetHandled;

      switch (message.Type) {
      case Message.MessageType.MethodCall:
        res = HandleMethod ((MethodCall) message);
	break;

      case Message.MessageType.Signal:
	// We're not interested in signals here because we're the ones
	// that generate them!
	break;
      }

      message.Dispose ();

      return (int) res;
    }
    
    private Result HandleMethod(MethodCall methodCall)
    {
      methodCall.Service = service;
      
      InterfaceProxy interfaceProxy = this.introspector.GetInterface(methodCall.InterfaceName);
      if (interfaceProxy == null || !interfaceProxy.HasMethod(methodCall.Key)) {
	// No such interface here.
	return Result.NotYetHandled;
      }
      
      MethodInfo method = interfaceProxy.GetMethod(methodCall.Key);
      
      Message.Push (methodCall);

      // Now call the method. FIXME: Error handling
      object [] args = methodCall.Arguments.GetParameters(method);
      object retVal = method.Invoke(this.handledObject, args);

      Message.Pop ();

      // Create the reply and send it
      MethodReturn methodReturn = new MethodReturn(methodCall);
      methodReturn.Arguments.AppendResults(method, retVal, args);
      methodReturn.Send();

      return Result.Handled;
    }

    internal string Path 
    {
      get 
	{
	  return path;
	}
    }

    internal Connection Connection 
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

    public Service Service
    {
      get
	{
	  return service;
	}
      
      set 
	{
	  this.service = value;
	}
    }
  }
}
