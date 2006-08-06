using System;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// Boolean
  /// </summary>
  public class Boolean : IDBusType
  {
    public const char Code = 'b';
    private System.Boolean val;
    
    private Boolean()
    {
    }
    
    public Boolean(System.Boolean val, Service service) 
    {
      this.val = val;
    }

    public Boolean(IntPtr iter, Service service)
    {
      dbus_message_iter_get_basic (iter, out this.val);
    }
    
    public void Append(IntPtr iter)
    {
      if (!dbus_message_iter_append_basic (iter, (int) Code, ref val))
	throw new ApplicationException("Failed to append BOOLEAN argument:" + val);
    }

    public static bool Suits(System.Type type) 
    {
      switch (type.ToString()) {
      case "System.Boolean":
      case "System.Boolean&":
	return true;
      }
      
      return false;
    }

    public static void EmitMarshalIn(ILGenerator generator, Type type)
    {
      if (type.IsByRef) {
	generator.Emit(OpCodes.Ldind_I1);
      }
    }

    public static void EmitMarshalOut(ILGenerator generator, Type type, bool isReturn) 
    {
      generator.Emit(OpCodes.Unbox, type);
      generator.Emit(OpCodes.Ldind_I1);
      if (!isReturn) {
	generator.Emit(OpCodes.Stind_I1);
      }
    }
    
    public object Get() 
    {
      return this.val;
    }

    public object Get(System.Type type)
    {
      switch (type.ToString()) {
      case "System.Boolean":
      case "System.Boolean&":
	return this.val;
      default:
	throw new ArgumentException("Cannot cast DBus.Type.Boolean to type '" + type.ToString() + "'");
      }
    }

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_get_basic (IntPtr iter, out bool value);
 
    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_append_basic (IntPtr iter, int type, ref bool value);
  }
}
