using System;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// An object path.
  /// </summary>
  public class ObjectPath : IDBusType
  {
    public const char Code = 'o';
    private string path = null;
    private object val = null;
    private Service service = null;
    
    private ObjectPath()
    {
    }
    
    public ObjectPath(object val, Service service) 
    {
      this.val = val;
      this.service = service;
    }
    
    public ObjectPath(IntPtr iter, Service service)
    {
      IntPtr raw;

      dbus_message_iter_get_basic (iter, out raw);

      this.path = Marshal.PtrToStringAnsi (raw);
      this.service = service;
    }

    private string Path
    {
      get {
	if (this.path == null && this.val != null) {
	  Handler handler = this.service.GetHandler(this.val);
	  this.path = handler.Path;
	}

	return this.path;
      }
    }

    public void Append(IntPtr iter) 
    {
      IntPtr marshalVal = Marshal.StringToHGlobalAnsi (Path);

      bool success = dbus_message_iter_append_basic (iter, (int) Code, ref marshalVal);
      Marshal.FreeHGlobal (marshalVal);

      if (!success)
	throw new ApplicationException("Failed to append OBJECT_PATH argument:" + val);
    }

    public static bool Suits(System.Type type) 
    {
      object[] attributes = type.GetCustomAttributes(typeof(InterfaceAttribute), false);
      if (attributes.Length == 1) {
	return true;
      } else {
	return false;
      }
    }

    public static void EmitMarshalIn(ILGenerator generator, Type type)
    {
      if (type.IsByRef) {
	generator.Emit(OpCodes.Ldind_Ref);
      }
    }

    public static void EmitMarshalOut(ILGenerator generator, Type type, bool isReturn) 
    {
      generator.Emit(OpCodes.Castclass, type);
      if (!isReturn) {
	generator.Emit(OpCodes.Stind_Ref);
      }
    }

    public object Get() 
    {
      throw new ArgumentException("Cannot call Get on an ObjectPath without specifying type.");
    }

    public object Get(System.Type type)
    {
      try {
	return this.service.GetObject(type, Path);
      } catch(Exception ex) {
	throw new ArgumentException("Cannot cast object pointed to by Object Path to type '" + type.ToString() + "': " + ex);
      }
    }

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_get_basic (IntPtr iter, out IntPtr path);
 
    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_append_basic (IntPtr iter, int type, ref IntPtr path);
  }
}
