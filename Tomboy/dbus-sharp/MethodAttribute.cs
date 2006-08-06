using System;

namespace DBus
{
  [AttributeUsage(AttributeTargets.Method, AllowMultiple=false, Inherited=true)]
  public class MethodAttribute : Attribute 
  {
    public MethodAttribute() 
    {
    }
  }
}
