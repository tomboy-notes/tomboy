namespace DBus
{
  using System;
  using System.Collections;
  using System.Reflection;
  
  internal class InterfaceProxy
  {
    private static Hashtable interfaceProxies = new Hashtable();
    private Hashtable methods = null;
    private Hashtable signals = null;
    
    private string interfaceName;

    private InterfaceProxy(Type type) 
    {
      object[] attributes = type.GetCustomAttributes(typeof(InterfaceAttribute), true);
      InterfaceAttribute interfaceAttribute = (InterfaceAttribute) attributes[0];
      this.interfaceName = interfaceAttribute.InterfaceName;
      AddMethods(type);
      AddSignals(type);
    }

    // Add all the events with Signal attributes
    private void AddSignals(Type type)
    {
      this.signals = new Hashtable();
      foreach (EventInfo signal in type.GetEvents(BindingFlags.Public |
						  BindingFlags.Instance |
						  BindingFlags.DeclaredOnly)) {
	object[] attributes = signal.GetCustomAttributes(typeof(SignalAttribute), false);
	if (attributes.GetLength(0) > 0) {
	  MethodInfo invoke = signal.EventHandlerType.GetMethod("Invoke");
	  signals.Add(signal.Name + " " + GetSignature(invoke), signal);
	}
      }      
    }

    // Add all the methods with Method attributes
    private void AddMethods(Type type)
    {
      this.methods = new Hashtable();
      foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | 
						    BindingFlags.Instance | 
						    BindingFlags.DeclaredOnly)) {
	object[] attributes = method.GetCustomAttributes(typeof(MethodAttribute), false);
	if (attributes.GetLength(0) > 0) {
	  methods.Add(method.Name + " " + GetSignature(method), method);
	}
      }
    }
    

    public static InterfaceProxy GetInterface(Type type) 
    {
      if (!interfaceProxies.Contains(type)) {
	interfaceProxies[type] = new InterfaceProxy(type);
      }

      return (InterfaceProxy) interfaceProxies[type];
    }

    public bool HasMethod(string key) 
    {
      return this.Methods.Contains(key);
    }

    public bool HasSignal(string key)
    {
      return this.Signals.Contains(key);
    }
    
    public EventInfo GetSignal(string key)
    {
      return (EventInfo) this.Signals[key];
    }
    
    public MethodInfo GetMethod(string key)
    {
      return (MethodInfo) this.Methods[key];
    }

    public static string GetSignature(MethodInfo method) 
    {
      ParameterInfo[] pars = method.GetParameters();
      string key = "";
      
      foreach (ParameterInfo par in pars) {
	if (!par.IsOut) {
	  Type dbusType = Arguments.MatchType(par.ParameterType);
	  key += Arguments.GetCode(dbusType);
	}
      }

      return key;
    }

    public Hashtable Methods
    {
      get {
	return this.methods;
      }
    }

    public Hashtable Signals
    {
      get {
	return this.signals;
      }
    }
    
    public string InterfaceName
    {
      get {
	return this.interfaceName;
      }
    }
  }
}

    
