namespace DBus 
{
  
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  using System.Collections;
  using System.Reflection;
  
  internal class Introspector
  {
    private Type type;
    private static Hashtable introspectors = new Hashtable();
    private Hashtable interfaceProxies = null;
    
    public static Introspector GetIntrospector(Type type) 
    {
      if (!introspectors.Contains(type)) {
	introspectors[type] = new Introspector(type);
      }

      return (Introspector) introspectors[type];
    }

    private Introspector(Type type) 
    {
      interfaceProxies = new Hashtable();
      AddType(type);
      this.type = type;
    }
    
    private void AddType(Type type) 
    {
      if (type == typeof(object)) {
	// Base case
	return;
      }

      object[] attributes = type.GetCustomAttributes(typeof(InterfaceAttribute), false);
      if (attributes.Length >= 1) {
	// This is a D-BUS interface so add it to the hashtable
	InterfaceProxy interfaceProxy = InterfaceProxy.GetInterface(type);
	interfaceProxies.Add(interfaceProxy.InterfaceName, interfaceProxy);
      }

      AddType(type.BaseType);
    }
    
    public InterfaceProxy GetInterface(string interfaceName) {
      if (interfaceProxies.Contains(interfaceName)) {
	return (InterfaceProxy) interfaceProxies[interfaceName];
      } else {
	return null;
      }
    }

    public Hashtable InterfaceProxies
    {
      get {
	return this.interfaceProxies;
      }
    }

    public ConstructorInfo Constructor
    {
      get {
	ConstructorInfo ret = this.type.GetConstructor(new Type[0]);
	if (ret != null) {
	  return ret;
	} else {
	  return typeof(object).GetConstructor(new Type[0]);
	}
      }
    }

    public override string ToString()
    {
      return this.type.ToString();
    }
  }
}
