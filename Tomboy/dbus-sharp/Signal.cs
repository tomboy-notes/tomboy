namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  public class Signal : Message
  {    
    public Signal() : base(MessageType.Signal)
    {  
    }

    internal Signal(IntPtr rawMessage, Service service) : base(rawMessage, service)
    {
    }

    public Signal(Service service) : base(MessageType.Signal, service) 
    {
    }

    public Signal(Service service, string pathName, string interfaceName, string name)
    {
      this.service = service;

      RawMessage = dbus_message_new_signal(pathName, interfaceName, name);
      
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
    private extern static IntPtr dbus_message_new_signal(string pathName, string interfaceName, string name);
  }
}
