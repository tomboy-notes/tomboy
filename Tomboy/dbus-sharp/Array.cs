using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection.Emit;

using DBus;

namespace DBus.DBusType
{
  /// <summary>
  /// Array.
  /// </summary>
  public class Array : IDBusType
  {
    public const char Code = 'a';
    private System.Array val;
    private ArrayList elements;
    private Type elementType;
    private Service service = null;

    private Array()
    {
    }
    
    public Array(System.Array val, Service service) 
    {
      this.val = val;
      this.elementType = Arguments.MatchType(val.GetType().GetElementType());
      this.service = service;
    }

    public Array(IntPtr iter, Service service)
    {
      this.service = service;

      IntPtr arrayIter = Marshal.AllocCoTaskMem(Arguments.DBusMessageIterSize);

      int elementTypeCode = dbus_message_iter_get_element_type (iter);
      dbus_message_iter_recurse (iter, arrayIter);
      this.elementType = (Type) Arguments.DBusTypes [(char) elementTypeCode];

      elements = new ArrayList ();

      if (dbus_message_iter_get_arg_type (arrayIter) != 0) {
        do {
          object [] pars = new Object[2];
	  pars[0] = arrayIter;
  	  pars[1] = service;
	  DBusType.IDBusType dbusType = (DBusType.IDBusType) Activator.CreateInstance(elementType, pars);
	  elements.Add(dbusType);
        } while (dbus_message_iter_next(arrayIter));
      }      

      Marshal.FreeCoTaskMem(arrayIter);
    }

    public string GetElementCodeAsString ()
    {
      string ret = System.String.Empty;
      Type t = val.GetType ().GetElementType ();

      while (true) {
        ret += Arguments.GetCodeAsString (Arguments.MatchType(t));

        if (t.IsArray)
          t = t.GetElementType ();
        else
          break;
      }
     
      return ret; 
    }
    
    public void Append(IntPtr iter)
    {
      IntPtr arrayIter = Marshal.AllocCoTaskMem (Arguments.DBusMessageIterSize);

      if (!dbus_message_iter_open_container (iter,
					     (int) Code, GetElementCodeAsString(),
					     arrayIter)) {
	throw new ApplicationException("Failed to append array argument: " + val);
      }
      
      foreach (object element in this.val) {
	object [] pars = new Object[2];
	pars[0] = element;
	pars[1] = this.service;
	DBusType.IDBusType dbusType = (DBusType.IDBusType) Activator.CreateInstance(elementType, pars);
	dbusType.Append(arrayIter);
      }

      if (!dbus_message_iter_close_container (iter, arrayIter)) {
	throw new ApplicationException ("Failed to append array argument: " + val);
      }

      Marshal.FreeCoTaskMem (arrayIter);
    }    

    public static bool Suits(System.Type type) 
    {
      Type type2 = type.GetElementType ();
      if (type.IsArray || (type2 != null && type2.IsArray)) {
	return true;
      }
      
      return false;
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
      throw new ArgumentException("Cannot call Get on an Array without specifying type.");
    }

    public object Get(System.Type type)
    {
      if (type.IsArray)
	type = type.GetElementType ();

      if (Arguments.Suits(elementType, type.UnderlyingSystemType)) {
	this.val = System.Array.CreateInstance(type.UnderlyingSystemType, elements.Count);
	int i = 0;
	foreach (DBusType.IDBusType element in elements) {
	  this.val.SetValue(element.Get(type.UnderlyingSystemType), i++);
	}	
      } else {
	throw new ArgumentException("Cannot cast DBus.Type.Array to type '" + type.ToString() + "'");
      }
	
	return this.val;
    }    

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_open_container (IntPtr iter,
								 int containerType,
								 string elementType,
								 IntPtr subIter);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_close_container (IntPtr iter,
								  IntPtr subIter);
 
    [DllImport("dbus-1")]
    private extern static int dbus_message_iter_get_element_type(IntPtr iter);

    [DllImport("dbus-1")]
    private extern static int dbus_message_iter_get_arg_type(IntPtr iter);

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_recurse(IntPtr iter, IntPtr subIter);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_next(IntPtr iter);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_has_next (IntPtr iter);
  }
}
