namespace DBus
{

  using System;

  public delegate void NameOwnerChangedHandler (string name,
						string oldOwner,
						string newOwner);

  [Interface ("org.freedesktop.DBus")]
  public abstract class BusDriver
  {
    [Method]
    public abstract string[] ListNames ();

    [Method]
    public abstract string GetNameOwner (string name);

    [Method]
    public abstract UInt32 GetConnectionUnixUser (string connectionName);


    [Signal]
    public virtual event NameOwnerChangedHandler NameOwnerChanged;

    static public BusDriver New (Connection connection)
    {
      Service service;
      service = Service.Get (connection, "org.freedesktop.DBus");

      BusDriver driver;
      driver = (BusDriver) service.GetObject (typeof (BusDriver), "/org/freedesktop/DBus");
      
      return driver;
    }
  }
}
