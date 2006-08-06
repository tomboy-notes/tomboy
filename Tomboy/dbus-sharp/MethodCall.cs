namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  public class MethodCall : Message
  {
    public MethodCall() : base(MessageType.MethodCall)
    {
    }
    
    internal MethodCall(IntPtr rawMessage, Service service) : base(rawMessage, service)
    {
    }

    public MethodCall(Service service) : base(MessageType.MethodCall, service)
    {
    }

    public MethodCall(Service service, string pathName, string interfaceName, string name)
    {
      this.service = service;

      RawMessage = dbus_message_new_method_call(service.Name, pathName, interfaceName, name);
      
      if (RawMessage == IntPtr.Zero) {
	throw new OutOfMemoryException();
      }
      
      this.pathName = pathName;
      this.interfaceName = interfaceName;
      this.name = name;

      dbus_message_unref(RawMessage);
    }
    
    public new string PathName
    {
      get
	{
	  return base.PathName;
	}

      set
	{
	  base.PathName = value;
	}
    }

    public new string InterfaceName
    {
      get
	{
	  return base.InterfaceName;
	}

      set
	{
	  base.InterfaceName = value;
	}
    }

    public new string Name
    {
      get
	{
	  return base.Name;
	}

      set
	{
	  base.Name = value;
	}
    }
    
    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_new_method_call(string serviceName, string pathName, string interfaceName, string name);
  }
}
