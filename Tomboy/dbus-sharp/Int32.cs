using System;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// 32-bit integer.
  /// </summary>
  public class Int32 : IDBusType
  {
    public const char Code = 'i';
    private System.Int32 val;
    
    private Int32()
    {
    }
    
    public Int32(System.Int32 val, Service service) 
    {
      this.val = val;
    }

    public Int32(IntPtr iter, Service service)
    {
      dbus_message_iter_get_basic (iter, out this.val);
    }
    
    public void Append(IntPtr iter)
    {
      if (!dbus_message_iter_append_basic (iter, (int) Code, ref val))
	throw new ApplicationException("Failed to append INT32 argument:" + val);
    }

    public static bool Suits(System.Type type) 
    {
      if (type.IsEnum && Enum.GetUnderlyingType (type) == typeof(System.Int32)) {
	return true;
      }
      
      switch (type.ToString()) {
      case "System.Int32":
      case "System.Int32&":
	return true;      }
      
      return false;
    }

    public static void EmitMarshalIn(ILGenerator generator, Type type)
    {
      if (type.IsByRef) {
	generator.Emit(OpCodes.Ldind_I4);
      }
    }

    public static void EmitMarshalOut(ILGenerator generator, Type type, bool isReturn) 
    {
      generator.Emit(OpCodes.Unbox, type);
      generator.Emit(OpCodes.Ldind_I4);
      if (!isReturn) {
	generator.Emit(OpCodes.Stind_I4);
      }
    }
    
    public object Get() 
    {
      return this.val;
    }

    public object Get(System.Type type)
    {
      if (type.IsEnum) {
	return Enum.ToObject(type, this.val);
      }
      
      switch (type.ToString()) {
      case "System.Int32":
      case "System.Int32&":
	return this.val;
      default:
	throw new ArgumentException("Cannot cast DBus.Type.Int32 to type '" + type.ToString() + "'");
      }
    }    

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_get_basic (IntPtr iter, out System.Int32 value);
 
    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_append_basic (IntPtr iter, int type, ref System.Int32 value);
  }
}
