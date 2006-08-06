using System;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// 16-bit integer.
  /// </summary>
  public class Int16 : IDBusType
  {
    public const char Code = 'n';
    private System.Int16 val;
    
    private Int16()
    {
    }
    
    public Int16(System.Int16 val, Service service) 
    {
      this.val = val;
    }

    public Int16(IntPtr iter, Service service)
    {
      dbus_message_iter_get_basic (iter, out this.val);
    }
    
    public void Append(IntPtr iter)
    {
      if (!dbus_message_iter_append_basic (iter, (int) Code, ref val))
	throw new ApplicationException("Failed to append INT16 argument:" + val);
    }

    public static bool Suits(System.Type type) 
    {
      if (type.IsEnum && Enum.GetUnderlyingType (type) == typeof(System.Int16)) {
	return true;
      }
      
      switch (type.ToString()) {
      case "System.Int16":
      case "System.Int16&":
	return true;      }
      
      return false;
    }

    public static void EmitMarshalIn(ILGenerator generator, Type type)
    {
      if (type.IsByRef) {
	generator.Emit(OpCodes.Ldind_I2);
      }
    }

    public static void EmitMarshalOut(ILGenerator generator, Type type, bool isReturn) 
    {
      generator.Emit(OpCodes.Unbox, type);
      generator.Emit(OpCodes.Ldind_I2);
      if (!isReturn) {
	generator.Emit(OpCodes.Stind_I2);
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
      case "System.Int16":
      case "System.Int16&":
	return this.val;
      default:
	throw new ArgumentException("Cannot cast DBus.Type.Int16 to type '" + type.ToString() + "'");
      }
    }    

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_get_basic (IntPtr iter, out System.Int16 value);
 
    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_append_basic (IntPtr iter, int type, ref System.Int16 value);
  }
}
