using System;

namespace DBus.DBusType
{
  /// <summary>
  /// Base class for DBusTypes
  /// </summary>
  public interface IDBusType
  {
    object Get();
    
    object Get(System.Type type);  

    void Append(IntPtr iter);
  }
}
