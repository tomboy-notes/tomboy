using System;

namespace DBus
{
  [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
  public class InterfaceAttribute : Attribute 
  {
    private string interfaceName;

    public InterfaceAttribute(string interfaceName) 
    {
      this.interfaceName = interfaceName;
    }

    public string InterfaceName
    {
      get
	{
	  return this.interfaceName;
	}
    }
  }
}
