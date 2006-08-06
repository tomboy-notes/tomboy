namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  
  public class ErrorMessage : Message
  {    
    public ErrorMessage() : base(MessageType.Error)
    {  
    }

    internal ErrorMessage(IntPtr rawMessage, Service service) : base(rawMessage, service)
    {
    }

    public ErrorMessage(Service service) : base(MessageType.Error, service) 
    {
    }

    public new string Name
    {
      get {
	if (this.name == null) {
	  this.name = Marshal.PtrToStringAnsi(dbus_message_get_error_name(RawMessage));
	}
	
	return this.name;
      }
      
      set {
	if (value != this.name) {
	  dbus_message_set_error_name(RawMessage, value);
	  this.name = value;
	}
      }
    }

    [DllImport("dbus-1")]
    private extern static bool dbus_message_set_error_name(IntPtr rawMessage, string name);

    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_get_error_name(IntPtr rawMessage);
  }
}
