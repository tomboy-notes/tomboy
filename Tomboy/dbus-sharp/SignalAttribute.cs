using System;

namespace DBus
{
  [AttributeUsage(AttributeTargets.Event, AllowMultiple=false, Inherited=true)]  public class SignalAttribute : Attribute
  {
    public SignalAttribute()
    {
    }
  }
}
