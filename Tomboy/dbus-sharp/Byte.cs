using System;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// Byte
  /// </summary>
  public class Byte : IDBusType
  {
    public const char Code = 'y';
    private System.Byte val;
    
    private Byte()
    {
    }
    
    public Byte(System.Byte val, Service service) 
    {
      this.val = val;
    }

    public Byte(System.Char val, Service service) 
    {
      this.val = (byte) val;
    }

    public Byte(IntPtr iter, Service service)
    {
      dbus_message_iter_get_basic (iter, out this.val);
      }
    
    public void Append(IntPtr iter)
    {
      if (!dbus_message_iter_append_basic (iter, (int) Code, ref val))
	throw new ApplicationException("Failed to append BYTE argument:" + val);
    }

    public static bool Suits(System.Type type) 
    {
      if (type.IsEnum && Enum.GetUnderlyingType (type) == typeof(System.Byte)) {
	return true;
      }

      switch (type.ToString()) {
      case "System.Byte":
      case "System.Byte&":
      case "System.Char":
      case "System.Char&":
	return true;
      }
      
      return false;
    }

    public static void EmitMarshalIn(ILGenerator generator, Type type)
    {
      if (type.IsByRef) {
	generator.Emit(OpCodes.Ldind_U1);
      }
    }

    public static void EmitMarshalOut(ILGenerator generator, Type type, bool isReturn) 
    {
      generator.Emit(OpCodes.Unbox, type);
      generator.Emit(OpCodes.Ldind_U1);
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
      if (type.IsEnum) {
	return Enum.ToObject(type, this.val);
      }

      switch (type.ToString()) {
      case "System.Byte":
      case "System.Byte&":
	return this.val;
      case "System.Char":
      case "System.Char&":
	char charVal = (char) this.val;
	return charVal;
      default:
	throw new ArgumentException("Cannot cast DBus.Type.Byte to type '" + type.ToString() + "'");
      }
    }

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_get_basic (IntPtr iter, out byte value);
 
    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_append_basic (IntPtr iter, int type, ref byte value);
  }
}
