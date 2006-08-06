namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  public class MethodReturn : Message
  {
    private MethodReturn() : base(MessageType.MethodReturn)
    {
    }    

    internal MethodReturn(IntPtr rawMessage, Service service) : base(rawMessage, service)
    {
    }
    
    public MethodReturn(MethodCall methodCall)
    {
      this.service = methodCall.Service;
      
      RawMessage = dbus_message_new_method_return(methodCall.RawMessage);
      
      if (RawMessage == IntPtr.Zero) {
	throw new OutOfMemoryException();
      }
      
      dbus_message_unref(RawMessage);
    }
    
    public new string PathName
    {
      get
	{
	  return base.PathName;
	}
    }

    public new string InterfaceName
    {
      get
	{
	  return base.InterfaceName;
	}
    }

    public new string Name
    {
      get
	{
	  return base.Name;
	}
    }
    
    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_new_method_return(IntPtr rawMessage);
  }
}
