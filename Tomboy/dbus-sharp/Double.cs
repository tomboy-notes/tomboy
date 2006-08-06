using System;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// IEEE 754 double
  /// </summary>
  public class Double : IDBusType
  {
    public const char Code = 'd';
    private System.Double val;
    
    private Double()
    {
    }
    
    public Double(System.Double val, Service service) 
    {
      this.val = val;
    }

    public Double(IntPtr iter, Service service)
    {
      dbus_message_iter_get_basic (iter, out this.val);
    }
    
    public void Append(IntPtr iter)
    {
      if (!dbus_message_iter_append_basic (iter, (int) Code, ref val))
	throw new ApplicationException("Failed to append DOUBLE argument:" + val);
    }

    public static bool Suits(System.Type type) 
    {
      switch (type.ToString()) {
      case "System.Double":
      case "System.Double&":
	return true;
      }
      
      return false;
    }

    public static void EmitMarshalIn(ILGenerator generator, Type type)
    {
      if (type.IsByRef) {
	generator.Emit(OpCodes.Ldind_R8);
      }
    }

    public static void EmitMarshalOut(ILGenerator generator, Type type, bool isReturn) 
    {
      generator.Emit(OpCodes.Unbox, type);
      generator.Emit(OpCodes.Ldind_R8);
      if (!isReturn) {
	generator.Emit(OpCodes.Stind_R8);
      }
    }
    
    public object Get() 
    {
      return this.val;
    }

    public object Get(System.Type type)
    {
      switch (type.ToString()) {
      case "System.Double":
      case "System.Double&":
	return this.val;
      default:
	throw new ArgumentException("Cannot cast DBus.Type.Double to type '" + type.ToString() + "'");
      }
    }

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_get_basic (IntPtr iter, out double value);
 
    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_append_basic (IntPtr iter, int type, ref double value);
  }
}
