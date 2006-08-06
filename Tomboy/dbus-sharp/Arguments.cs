using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DBus
{
  // Holds the arguments of a message. Provides methods for appending
  // arguments and to assist in matching .NET types with D-BUS types.
	public class Arguments : IEnumerable, IDisposable
  {
    // Must follow sizeof(DBusMessageIter)
    internal const int DBusMessageIterSize = 14*4;
    private static Hashtable dbusTypes = null;
    private Message message;
    private IntPtr appenderIter;
    private IEnumerator enumerator = null;
    
    internal Arguments (Message message)
    {
      this.appenderIter = Marshal.AllocCoTaskMem(DBusMessageIterSize);
      this.message = message;
    }

    private void Dispose (bool disposing)
    {
      Marshal.FreeCoTaskMem(appenderIter);
    }

    public void Dispose ()
    {
      Dispose (true);
      GC.SuppressFinalize (this);
    }

    ~Arguments()
    {
      Dispose (false);
    }

    // Checks the suitability of a D-BUS type for supporting a .NET
    // type.
    public static bool Suits(Type dbusType, Type type) 
    {
      object [] pars = new object[1];
      pars[0] = type;
      
      return (bool) dbusType.InvokeMember("Suits", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, pars, null);
    }
    
    // Find a suitable match for the given .NET type or throw an
    // exception if one can't be found.
    public static Type MatchType(Type type) 
    {      
      foreach(Type dbusType in DBusTypes.Values) {
	if (Suits(dbusType, type)) {
	  return dbusType;
	}
      }
      
      throw new ApplicationException("No suitable DBUS type found for type '" + type + "'");
    }
    
    // The D-BUS types.
    public static Hashtable DBusTypes {
      get 
	{
	  if (dbusTypes == null) {
	    dbusTypes = new Hashtable();

	    foreach (Type type in Assembly.GetAssembly(typeof(DBusType.IDBusType)).GetTypes()) {
	      if (type != typeof(DBusType.IDBusType) && typeof(DBusType.IDBusType).IsAssignableFrom(type)) {
		dbusTypes.Add(GetCode(type), type);
	      }
	    }
	  }
	  
	  return dbusTypes;
	}
    }
    
    // Append an argument
    public void Append(DBusType.IDBusType dbusType)
    {
      dbusType.Append(appenderIter);
    }
    
    // Append an argument of the specified type
    private void AppendType(Type type, object val)
    {
      object [] pars = new Object[2];
      pars[0] = val;
      pars[1] = message.Service;
      DBusType.IDBusType dbusType = (DBusType.IDBusType) Activator.CreateInstance(MatchType(type), pars);
      Append(dbusType);
    }
    
    // Append the results of a method call
    public void AppendResults(MethodInfo method, object retVal, object [] parameters) 
    {
      InitAppending();

      if (method.ReturnType != typeof(void)) {
	AppendType(method.ReturnType, retVal);
      }
      
      for (int i = 0; i < method.GetParameters().Length; i++) {
	ParameterInfo par = method.GetParameters()[i];
	if (par.IsOut || par.ParameterType.ToString().EndsWith("&")) {
	  // It's an OUT or INOUT parameter.
	  AppendType(par.ParameterType.UnderlyingSystemType, parameters[i]);
	}
      }
    }
    
    // Get the parameters
    public object[] GetParameters(MethodInfo method) 
    {
      ParameterInfo[] pars = method.GetParameters();
      ArrayList paramList = new ArrayList();
      
      enumerator = GetEnumerator();
      foreach (ParameterInfo par in pars) {
	if (!par.IsOut) {
	  // It's an IN or INOUT paramter.
	  enumerator.MoveNext();
	  DBusType.IDBusType dbusType = (DBusType.IDBusType) enumerator.Current;
	  paramList.Add(dbusType.Get(par.ParameterType));
	} else {
	  // It's an OUT so just create a parameter for it
	  object var = null;
	  paramList.Add(var);
	}
      }
      
      return paramList.ToArray();
    }

    // Parse the IN & REF parameters to a method and return the types in a list.
    public static object[] ParseInParameters(MethodInfo method)
    {
      ArrayList types = new ArrayList();

      ParameterInfo[] pars = method.GetParameters();
      foreach (ParameterInfo par in pars) {
	if (!par.IsOut) {
	  types.Add(MatchType(par.ParameterType));
	}
      }

      return types.ToArray();
    }

    // Parse the OUT & REF parameters to a method and return the types in a list.
    public static object[] ParseOutParameters(MethodInfo method)
    {
      ArrayList types = new ArrayList();

      ParameterInfo[] pars = method.GetParameters();
      foreach (ParameterInfo par in pars) {
	if (par.IsOut || par.ParameterType.ToString().EndsWith("&")) {
	  types.Add(MatchType(par.ParameterType));
	}
      }

      return types.ToArray();
    }
    
    // Get the appropriate constructor for a D-BUS type
    public static ConstructorInfo GetDBusTypeConstructor(Type dbusType, Type type) 
    {
      Type constructorType;

      if (type.IsArray)
        constructorType = typeof (System.Array);
      else if (type.IsEnum)
        constructorType = Enum.GetUnderlyingType (type);
      else
        constructorType = type.UnderlyingSystemType;

      ConstructorInfo constructor = dbusType.GetConstructor(new Type[] {constructorType, typeof(Service)});
      if (constructor == null)
	throw new ArgumentException("There is no valid constructor for '" + dbusType + "' from type '" + type + "'");
      
      return constructor;
    }

    // Get the type code for a given D-BUS type
    public static char GetCode(Type dbusType) 
    {
      return (char) dbusType.InvokeMember("Code", BindingFlags.Static | BindingFlags.GetField, null, null, null);
    }

    // Get the type code for a given D-BUS type as a string
    public static string GetCodeAsString (Type dbusType)
    {
      return GetCode (dbusType).ToString ();
    }

    // Get a complete method signature
    public override string ToString() 
    {
      IntPtr iter = Marshal.AllocCoTaskMem(DBusMessageIterSize);
      string key = "";

      // Iterate through the parameters getting the type codes to a string
      bool notEmpty = dbus_message_iter_init(message.RawMessage, iter);

      if (notEmpty) {
	do {
	  char code = (char) dbus_message_iter_get_arg_type(iter);
	  if (code == '\0')
	    return key;
	  
	  key += code;
	} while (dbus_message_iter_next(iter));
      }

      Marshal.FreeCoTaskMem(iter);

      return key;
    }
    
    // Move to the next parameter
    public DBusType.IDBusType GetNext() 
    {
      enumerator.MoveNext();
      return (DBusType.IDBusType) enumerator.Current;
    }

    // Begin appending
    public void InitAppending() 
    {
      dbus_message_iter_init_append(message.RawMessage, appenderIter);
    }

    // Get the enumerator
    public IEnumerator GetEnumerator()
    {
      return new ArgumentsEnumerator(this);
    }

    private class ArgumentsEnumerator : IEnumerator
    {
      private Arguments arguments;
      private bool started = false;
      private bool notEmpty = false;
      private IntPtr iter = Marshal.AllocCoTaskMem(Arguments.DBusMessageIterSize);
      
      public ArgumentsEnumerator(Arguments arguments)
      {
	this.arguments = arguments;
	Reset();
      }
      
      ~ArgumentsEnumerator()
      {
	Marshal.FreeCoTaskMem(iter);
      }

      public bool MoveNext()
      {
	if (started) {
	  return dbus_message_iter_next(iter);
	} else {
	  started = true;
	  return notEmpty;
	}
      }
      
      public void Reset()
      {
	notEmpty = dbus_message_iter_init(arguments.message.RawMessage, iter);
	started = false;
      }
      
      public object Current
      {
	get
	  {
	    object [] pars = new Object[2];
	    pars[0] = iter;
	    pars[1] = arguments.message.Service;
	    
	    Type type = (Type) DBusTypes[(char) dbus_message_iter_get_arg_type(iter)];
	    DBusType.IDBusType dbusType = (DBusType.IDBusType) Activator.CreateInstance(type, pars);

	    return dbusType;
	  }
      }
    }

    [DllImport("dbus-1")]
    private extern static void dbus_message_iter_init_append(IntPtr rawMessage, IntPtr iter);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_has_next(IntPtr iter);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_next(IntPtr iter);

    [DllImport("dbus-1")]
    private extern static bool dbus_message_iter_init(IntPtr rawMessage, IntPtr iter);

    [DllImport("dbus-1")]
    private extern static int dbus_message_iter_get_arg_type(IntPtr iter);
  }
}
