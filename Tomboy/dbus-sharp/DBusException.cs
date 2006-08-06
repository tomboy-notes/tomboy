namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  
  public class DBusException : ApplicationException 
  {
    internal DBusException (Error error) : base (error.Message) { 
      error.Free();
    }
  }
}
