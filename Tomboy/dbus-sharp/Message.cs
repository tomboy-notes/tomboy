namespace DBus 
{
  
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  using System.Collections;
  
  public class Message : IDisposable
  {
    private static Stack stack = new Stack ();
	  
    static public Message Current {
      get 
	{
	  return stack.Count > 0 ? (Message) stack.Peek () : null;
	}
    }

    static internal void Push (Message message)
    {
      stack.Push (message);
    }

    static internal void Pop ()
    {
      stack.Pop ();
    }
	  
    
    /// <summary>
    /// A pointer to the underlying Message structure
    /// </summary>
    private IntPtr rawMessage;
    
    /// <summary>
    /// The current slot number
    /// </summary>
    private static int slot = -1;
    
    // Keep in sync with C
    public enum MessageType 
    {
      Invalid = 0,
      MethodCall = 1,
      MethodReturn = 2,
      Error = 3,
      Signal = 4
    }

    private Arguments arguments = null;

    protected Service service = null;
    protected string pathName = null;
    protected string interfaceName = null;
    protected string name = null;    
    private string key= null;

    protected Message()
    {
      // An empty constructor for the sake of sub-classes which know how to construct theirselves.
    }
    
    protected Message(IntPtr rawMessage, Service service)
    {
      RawMessage = rawMessage;
      this.service = service;
    }
    
    protected Message(MessageType messageType) 
    {
      // the assignment bumps the refcount
      RawMessage = dbus_message_new((int) messageType);
      
      if (RawMessage == IntPtr.Zero) {
	throw new OutOfMemoryException();
      }
      
      dbus_message_unref(RawMessage);
    }
    
    protected Message(MessageType messageType, Service service) : this(messageType) 
    {
      this.service = service;
    }

    public void Dispose() 
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }
    
    public void Dispose (bool disposing) 
    {
      if (disposing) {
        if (this.arguments != null)
	  this.arguments.Dispose ();
      }

      RawMessage = IntPtr.Zero; // free the native object
    }     

    ~Message() 
    {
      Dispose (false);
    }
    
    public static Message Wrap(IntPtr rawMessage, Service service) 
    {
      if (slot > -1) {
	// If we already have a Message object associated with this rawMessage then return it
	IntPtr rawThis = dbus_message_get_data(rawMessage, slot);
	if (rawThis != IntPtr.Zero && ((GCHandle)rawThis).Target == typeof(DBus.Message))
	  return (DBus.Message) ((GCHandle)rawThis).Target;
      } 
      // If it doesn't exist then create a new Message around it
      Message message = null;
      MessageType messageType = (MessageType) dbus_message_get_type(rawMessage);
      
      switch (messageType) {
      case MessageType.Signal:
	message = new Signal(rawMessage, service);
	break;
      case MessageType.MethodCall:
	message = new MethodCall(rawMessage, service);
	break;
      case MessageType.MethodReturn:
	message = new MethodReturn(rawMessage, service);
	break;
      case MessageType.Error:
	message = new ErrorMessage(rawMessage, service);
	break;
      default:
	throw new ApplicationException("Unknown message type to wrap: " + messageType);
      }

      return message;
    }
    
    internal IntPtr RawMessage 
    {
      get 
	{
	  return rawMessage;
	}
      set 
	{
	  if (value == rawMessage) 
	    return;
	  
	  if (rawMessage != IntPtr.Zero) 
	    {
	      // Get the reference to this
	      IntPtr rawThis = dbus_message_get_data(rawMessage, Slot);
	      Debug.Assert (rawThis != IntPtr.Zero);
	      
	      // Blank over the reference
	      dbus_message_set_data(rawMessage, Slot, IntPtr.Zero, IntPtr.Zero);
	      
	      // Free the reference
	      ((GCHandle) rawThis).Free();
	      
	      // Unref the connection
	      dbus_message_unref(rawMessage);
	    }
	  
	  this.rawMessage = value;
	  
	  if (rawMessage != IntPtr.Zero) 
	    {
	      GCHandle rawThis;
	      
	      dbus_message_ref(rawMessage);
	      
	      // We store a weak reference to the C# object on the C object
	      rawThis = GCHandle.Alloc(this, GCHandleType.WeakTrackResurrection);
	      
	      dbus_message_set_data(rawMessage, Slot, (IntPtr) rawThis, IntPtr.Zero);
	    }
	}
    }
    
    public void Send(ref int serial) 
    {
      if (!dbus_connection_send (Service.Connection.RawConnection, RawMessage, ref serial))
	throw new OutOfMemoryException ();

      Service.Connection.Flush();
    }
    
    public void Send() 
    {
      int ignored = 0;
      Send(ref ignored);
    }

    public void SendWithReply() 
    {
      IntPtr rawPendingCall = IntPtr.Zero;
      
      if (!dbus_connection_send_with_reply (Service.Connection.RawConnection, RawMessage, rawPendingCall, Service.Connection.Timeout))
	throw new OutOfMemoryException();
    }
     
    public MethodReturn SendWithReplyAndBlock()
    {
      Error error = new Error();
      error.Init();

      IntPtr rawMessage = dbus_connection_send_with_reply_and_block(Service.Connection.RawConnection, 
								    RawMessage, 
								    Service.Connection.Timeout, 
								    ref error);

      if (rawMessage != IntPtr.Zero) {
	MethodReturn methodReturn = new MethodReturn(rawMessage, Service);
	// Ownership of a ref is passed onto us from
	// dbus_connection_send_with_reply_and_block().  It gets reffed as
	// a result of being passed into the MethodReturn ctor, so unref
	// the extra one here.
	dbus_message_unref (rawMessage);

	return methodReturn;
      } else {
	throw new DBusException(error);
      }
    }

    public MessageType Type
    {
      get 
	{
	  return (MessageType) dbus_message_get_type(RawMessage);
	}
    }
    
    public Service Service
    {
      set 
	{
	  if (this.service != null && (value.Name != this.service.Name)) {
	    if (!dbus_message_set_destination(RawMessage, value.Name)) {
	      throw new OutOfMemoryException();
	    }
	  }
	  
	  this.service = value;
	}
      get 
	{
	  return this.service;
	}
    }
    
    protected virtual string PathName
    {
      set 
	{
	  if (value != this.pathName) 
	    {
	      if (!dbus_message_set_path(RawMessage, value)) {
		throw new OutOfMemoryException();
	      }
	      
	      this.pathName = value;
	    }
	}
      get 
	{
	  if (this.pathName == null) {
	    this.pathName = Marshal.PtrToStringAnsi(dbus_message_get_path(RawMessage));
	  }
	  
	  return this.pathName;
	}
    }
    
    protected virtual string InterfaceName
    {
      set 
	{
	  if (value != this.interfaceName)
	    {
	      dbus_message_set_interface (RawMessage, value);
	      this.interfaceName = value;
	    }
	}
      get 
	{
	  if (this.interfaceName == null) {
	    this.interfaceName = Marshal.PtrToStringAnsi(dbus_message_get_interface(RawMessage));
	  }

	  return this.interfaceName;
	}
    }
    
    protected virtual string Name
    {
      set {
	if (value != this.name) {
	  dbus_message_set_member(RawMessage, value);
	  this.name = value;
	}
      }
      get {
	if (this.name == null) {
	  this.name = Marshal.PtrToStringAnsi(dbus_message_get_member(RawMessage));
	}

	return this.name;
      }
    }

    public string Key
    {
      get {
	if (this.key == null) {
	  this.key = Name + " " + Arguments;
	}
	
	return this.key;
      }
    }

    public Arguments Arguments
    {
      get 
	{
	  if (this.arguments == null) {
	    this.arguments = new Arguments(this);
	  }
	  
	  return this.arguments;
	}
    }

    public string Sender 
    {
      get
	{
	  return Marshal.PtrToStringAnsi(dbus_message_get_sender(RawMessage));
	}
    }

    public string Destination
    {
      get
	{
	  return Marshal.PtrToStringAnsi(dbus_message_get_destination(RawMessage));
	}
    }
	    
    protected int Slot
    {
      get 
	{
	  if (slot == -1) 
	    {
	      // We need to initialize the slot
	      if (!dbus_message_allocate_data_slot (ref slot))
		throw new OutOfMemoryException ();
	      
	      Debug.Assert (slot >= 0);
	    }
	  
	  return slot;
	}
    }
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_new")]
    protected extern static IntPtr dbus_message_new (int messageType);
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_unref")]
    protected extern static void dbus_message_unref (IntPtr ptr);
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_ref")]
    protected extern static void dbus_message_ref (IntPtr ptr);
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_allocate_data_slot")]
    protected extern static bool dbus_message_allocate_data_slot (ref int slot);
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_free_data_slot")]
    protected extern static void dbus_message_free_data_slot (ref int slot);
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_set_data")]
    protected extern static bool dbus_message_set_data (IntPtr ptr,
							int    slot,
							IntPtr data,
							IntPtr free_data_func);
    
    [DllImport ("dbus-1", EntryPoint="dbus_message_get_data")]
    protected extern static IntPtr dbus_message_get_data (IntPtr ptr,
							  int    slot);
    
    [DllImport ("dbus-1", EntryPoint="dbus_connection_send")]
    private extern static bool dbus_connection_send (IntPtr  ptr,
						     IntPtr  message,
						     ref int client_serial);

    [DllImport ("dbus-1", EntryPoint="dbus_connection_send_with_reply")]
    private extern static bool dbus_connection_send_with_reply (IntPtr rawConnection, IntPtr rawMessage, IntPtr rawPendingCall, int timeout);

    [DllImport ("dbus-1", EntryPoint="dbus_connection_send_with_reply_and_block")]
    private extern static IntPtr dbus_connection_send_with_reply_and_block (IntPtr rawConnection, IntPtr  message, int timeout, ref Error error);

    [DllImport("dbus-1")]
    private extern static int dbus_message_get_type(IntPtr rawMessage);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_set_path(IntPtr rawMessage, string pathName);

    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_get_path(IntPtr rawMessage);
    
    [DllImport("dbus-1")]
    private extern static bool dbus_message_set_interface (IntPtr rawMessage, string interfaceName);

    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_get_interface(IntPtr rawMessage);
    
    [DllImport("dbus-1")]
    private extern static bool dbus_message_set_member(IntPtr rawMessage, string name);

    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_get_member(IntPtr rawMessage);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_set_destination(IntPtr rawMessage, string serviceName);

    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_get_destination(IntPtr rawMessage);

    [DllImport("dbus-1")]
    private extern static IntPtr dbus_message_get_sender(IntPtr rawMessage);
  }
}
