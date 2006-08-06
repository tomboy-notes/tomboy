namespace DBus
{
  using System;
  using System.Runtime.InteropServices;
  using System.Diagnostics;
  using System.Collections;
  using System.Threading;
  using System.Reflection;
  using System.Reflection.Emit;

  internal class ProxyBuilder
  {
    private Service service= null;
    private string pathName = null;
    private Type type = null;
    private Introspector introspector = null;
    
    private static MethodInfo Service_NameMI = typeof(Service).GetMethod("get_Name", 
									    new Type[0]);
    private static MethodInfo Service_ConnectionMI = typeof(Service).GetMethod("get_Connection",
										  new Type[0]);
    private static MethodInfo Service_AddSignalCalledMI = typeof(Service).GetMethod("add_SignalCalled",
										    new Type[] {typeof(Service.SignalCalledHandler)});
    private static MethodInfo Service_RemoveSignalCalledMI = typeof(Service).GetMethod("remove_SignalCalled",
										    new Type[] {typeof(Service.SignalCalledHandler)});										    
    private static MethodInfo Signal_PathNameMI = typeof(Signal).GetMethod("get_PathName",
									   new Type[0]);
    private static MethodInfo Message_ArgumentsMI = typeof(Message).GetMethod("get_Arguments",
										 new Type[0]);
    private static MethodInfo Message_KeyMI = typeof(Message).GetMethod("get_Key",
									new Type[0]);
    private static MethodInfo Arguments_InitAppendingMI = typeof(Arguments).GetMethod("InitAppending",
											  new Type[0]);
    private static MethodInfo Arguments_AppendMI = typeof(Arguments).GetMethod("Append",
										  new Type[] {typeof(DBusType.IDBusType)});
    private static MethodInfo Message_SendWithReplyAndBlockMI = typeof(Message).GetMethod("SendWithReplyAndBlock",
											     new Type[0]);
    private static MethodInfo Message_SendMI = typeof(Message).GetMethod("Send",
									 new Type[0]);
    private static MethodInfo Message_DisposeMI = typeof(Message).GetMethod("Dispose",
									    new Type[0]);
    private static MethodInfo Arguments_GetEnumeratorMI = typeof(Arguments).GetMethod("GetEnumerator",
											  new Type[0]);
    private static MethodInfo IEnumerator_MoveNextMI = typeof(System.Collections.IEnumerator).GetMethod("MoveNext",
													new Type[0]);
    private static MethodInfo IEnumerator_CurrentMI = typeof(System.Collections.IEnumerator).GetMethod("get_Current",
												       new Type[0]);
    private static MethodInfo Type_GetTypeFromHandleMI = typeof(System.Type).GetMethod("GetTypeFromHandle",
										       new Type[] {typeof(System.RuntimeTypeHandle)});
    private static MethodInfo IDBusType_GetMI = typeof(DBusType.IDBusType).GetMethod("Get",
										     new Type[] {typeof(System.Type)});
    private static ConstructorInfo MethodCall_C = typeof(MethodCall).GetConstructor(new Type[] {typeof(Service),
												typeof(string),
												typeof(string),
												typeof(string)});
    private static ConstructorInfo Signal_C = typeof(Signal).GetConstructor(new Type[] {typeof(Service),
											typeof(string),
											typeof(string),
											typeof(string)});
    private static ConstructorInfo Service_SignalCalledHandlerC = typeof(Service.SignalCalledHandler).GetConstructor(new Type[] {typeof(object),
																 typeof(System.IntPtr)});
    private static MethodInfo String_opEqualityMI = typeof(System.String).GetMethod("op_Equality",
										    new Type[] {typeof(string),
												typeof(string)});													     
    private static MethodInfo MulticastDelegate_opInequalityMI = typeof(System.MulticastDelegate).GetMethod("op_Inequality",
										    new Type[] {typeof(System.MulticastDelegate),
												typeof(System.MulticastDelegate)});
    

    public ProxyBuilder(Service service, Type type, string pathName)
    {
      this.service = service;
      this.pathName = pathName;
      this.type = type;
      this.introspector = Introspector.GetIntrospector(type);
    }

    private MethodInfo BuildSignalCalled(ref TypeBuilder typeB, FieldInfo serviceF, FieldInfo pathF)
    {
      Type[] parTypes = {typeof(Signal)};
      MethodBuilder methodBuilder = typeB.DefineMethod("Service_SignalCalled",
						       MethodAttributes.Private |
						       MethodAttributes.HideBySig,
						       typeof(void),
						       parTypes);
      
      ILGenerator generator = methodBuilder.GetILGenerator();

      LocalBuilder enumeratorL = generator.DeclareLocal(typeof(System.Collections.IEnumerator));
      enumeratorL.SetLocalSymInfo("enumerator");

      Label wrongPath = generator.DefineLabel();
      //generator.EmitWriteLine("if (signal.PathName == pathName) {");
      generator.Emit(OpCodes.Ldarg_1);
      generator.EmitCall(OpCodes.Callvirt, Signal_PathNameMI, null);
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldfld, pathF);
      generator.EmitCall(OpCodes.Call, String_opEqualityMI, null);
      generator.Emit(OpCodes.Brfalse, wrongPath);

      int localOffset = 1;

      foreach (DictionaryEntry interfaceEntry in this.introspector.InterfaceProxies) {
	InterfaceProxy interfaceProxy = (InterfaceProxy) interfaceEntry.Value;
	foreach (DictionaryEntry signalEntry in interfaceProxy.Signals) {
	  EventInfo eventE = (EventInfo) signalEntry.Value;
	  // This is really cheeky since we need to grab the event as a private field.
	  FieldInfo eventF = this.type.GetField(eventE.Name,
						BindingFlags.NonPublic|
						BindingFlags.Instance);

	  MethodInfo eventHandler_InvokeMI = eventE.EventHandlerType.GetMethod("Invoke");

	  ParameterInfo[] pars = eventHandler_InvokeMI.GetParameters();
	  parTypes = new Type[pars.Length];
	  for (int parN = 0; parN < pars.Length; parN++) {
	    parTypes[parN] = pars[parN].ParameterType;
	    LocalBuilder parmL = generator.DeclareLocal(parTypes[parN]);
	    parmL.SetLocalSymInfo(pars[parN].Name);
	  }
	  
	  Label skip = generator.DefineLabel();      
	  //generator.EmitWriteLine("  if (SelectedIndexChanged != null) {");
	  generator.Emit(OpCodes.Ldarg_0);
	  generator.Emit(OpCodes.Ldfld, eventF);
	  generator.Emit(OpCodes.Ldnull);
	  generator.EmitCall(OpCodes.Call, MulticastDelegate_opInequalityMI, null);
	  generator.Emit(OpCodes.Brfalse, skip);
	  
	  //generator.EmitWriteLine("    if (signal.Key == 'la i')");
	  generator.Emit(OpCodes.Ldarg_1);
	  generator.EmitCall(OpCodes.Callvirt, Message_KeyMI, null);
	  generator.Emit(OpCodes.Ldstr, eventE.Name + " " + InterfaceProxy.GetSignature(eventHandler_InvokeMI));
	  generator.EmitCall(OpCodes.Call, String_opEqualityMI, null);
	  generator.Emit(OpCodes.Brfalse, skip);

	  //generator.EmitWriteLine("IEnumerator enumerator = signal.Arguments.GetEnumerator()");
	  generator.Emit(OpCodes.Ldarg_1);
	  generator.EmitCall(OpCodes.Callvirt, Message_ArgumentsMI, null);
	  generator.EmitCall(OpCodes.Callvirt, Arguments_GetEnumeratorMI, null);
	  generator.Emit(OpCodes.Stloc_0);
	  
	  for (int parN = 0; parN < pars.Length; parN++) {
	    ParameterInfo par = pars[parN];
	    if (!par.IsOut) {
	      EmitSignalIn(generator, par.ParameterType, parN + localOffset, serviceF);
	    }
	  }
	  
	  //generator.EmitWriteLine("    SelectedIndexChanged(selectedIndex)");
	  generator.Emit(OpCodes.Ldarg_0);
	  generator.Emit(OpCodes.Ldfld, eventF);
	  for (int parN = 0; parN < pars.Length; parN++) {
	    generator.Emit(OpCodes.Ldloc_S, parN + localOffset);
	  }
	  
	  generator.EmitCall(OpCodes.Callvirt, eventHandler_InvokeMI, null);
	  
	  generator.MarkLabel(skip);
	  //generator.EmitWriteLine("  }");
	  
	  localOffset += pars.Length;
	}
      }

      generator.MarkLabel(wrongPath);
      //generator.EmitWriteLine("}");

      //generator.EmitWriteLine("return");
      generator.Emit(OpCodes.Ret);

      return methodBuilder;
    }
    
    private void BuildSignalHandler(EventInfo eventE, 
				    InterfaceProxy interfaceProxy,
				    ref TypeBuilder typeB, 
				    FieldInfo serviceF,
				    FieldInfo pathF)
    {
      MethodInfo eventHandler_InvokeMI = eventE.EventHandlerType.GetMethod("Invoke");
      ParameterInfo[] pars = eventHandler_InvokeMI.GetParameters();
      Type[] parTypes = new Type[pars.Length];
      for (int parN = 0; parN < pars.Length; parN++) {
	parTypes[parN] = pars[parN].ParameterType;
      }

      // Generate the code
      MethodBuilder methodBuilder = typeB.DefineMethod("Proxy_" + eventE.Name, 
						       MethodAttributes.Public |
						       MethodAttributes.HideBySig |
						       MethodAttributes.Virtual, 
						       typeof(void),
						       parTypes);
      ILGenerator generator = methodBuilder.GetILGenerator();

      for (int parN = 0; parN < pars.Length; parN++) {
	methodBuilder.DefineParameter(parN + 1, pars[parN].Attributes, pars[parN].Name);
      }

      // Generate the locals
      LocalBuilder methodCallL = generator.DeclareLocal(typeof(MethodCall));
      methodCallL.SetLocalSymInfo("signal");

      //generator.EmitWriteLine("Signal signal = new Signal(...)");
      generator.Emit(OpCodes.Ldsfld, serviceF);
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldfld, pathF);
      generator.Emit(OpCodes.Ldstr, interfaceProxy.InterfaceName);
      generator.Emit(OpCodes.Ldstr, eventE.Name);
      generator.Emit(OpCodes.Newobj, Signal_C);
      generator.Emit(OpCodes.Stloc_0);

      //generator.EmitWriteLine("signal.Arguments.InitAppending()");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_ArgumentsMI, null);
      generator.EmitCall(OpCodes.Callvirt, Arguments_InitAppendingMI, null);

      for (int parN = 0; parN < pars.Length; parN++) {
	ParameterInfo par = pars[parN];
	if (!par.IsOut) {
	  EmitIn(generator, par.ParameterType, parN, serviceF);
	}
      }
      
      //generator.EmitWriteLine("signal.Send()");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_SendMI, null); 

      //generator.EmitWriteLine("signal.Dispose()");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_DisposeMI, null);

      //generator.EmitWriteLine("return");
      generator.Emit(OpCodes.Ret);
    }

    private void BuildMethod(MethodInfo method, 
			     InterfaceProxy interfaceProxy,
			     ref TypeBuilder typeB, 
			     FieldInfo serviceF,
			     FieldInfo pathF)
    {
      ParameterInfo[] pars = method.GetParameters();
      Type[] parTypes = new Type[pars.Length];
      for (int parN = 0; parN < pars.Length; parN++) {
	parTypes[parN] = pars[parN].ParameterType;
      }

      // Generate the code
      MethodBuilder methodBuilder = typeB.DefineMethod(method.Name, 
						       MethodAttributes.Public |
						       MethodAttributes.HideBySig |
						       MethodAttributes.Virtual, 
						       method.ReturnType, 
						       parTypes);
      ILGenerator generator = methodBuilder.GetILGenerator();

      for (int parN = 0; parN < pars.Length; parN++) {
	methodBuilder.DefineParameter(parN + 1, pars[parN].Attributes, pars[parN].Name);
      }

      // Generate the locals
      LocalBuilder methodCallL = generator.DeclareLocal(typeof(MethodCall));
      methodCallL.SetLocalSymInfo("methodCall");
      LocalBuilder replyL = generator.DeclareLocal(typeof(MethodReturn));
      replyL.SetLocalSymInfo("reply");
      LocalBuilder enumeratorL = generator.DeclareLocal(typeof(System.Collections.IEnumerator));
      enumeratorL.SetLocalSymInfo("enumerator");

      if (method.ReturnType != typeof(void)) {
	LocalBuilder retvalL = generator.DeclareLocal(method.ReturnType);
	retvalL.SetLocalSymInfo("retval");
      }

      //generator.EmitWriteLine("MethodCall methodCall = new MethodCall(...)");
      generator.Emit(OpCodes.Ldsfld, serviceF);
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldfld, pathF);
      generator.Emit(OpCodes.Ldstr, interfaceProxy.InterfaceName);
      generator.Emit(OpCodes.Ldstr, method.Name);
      generator.Emit(OpCodes.Newobj, MethodCall_C);
      generator.Emit(OpCodes.Stloc_0);

      //generator.EmitWriteLine("methodCall.Arguments.InitAppending()");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_ArgumentsMI, null);
      generator.EmitCall(OpCodes.Callvirt, Arguments_InitAppendingMI, null);

      for (int parN = 0; parN < pars.Length; parN++) {
	ParameterInfo par = pars[parN];
	if (!par.IsOut) {
	  EmitIn(generator, par.ParameterType, parN, serviceF);
	}
      }
      
      //generator.EmitWriteLine("MethodReturn reply = methodCall.SendWithReplyAndBlock()");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_SendWithReplyAndBlockMI, null);      
      generator.Emit(OpCodes.Stloc_1);

      //generator.EmitWriteLine("IEnumerator enumeartor = reply.Arguments.GetEnumerator()");
      generator.Emit(OpCodes.Ldloc_1);
      generator.EmitCall(OpCodes.Callvirt, Message_ArgumentsMI, null);
      generator.EmitCall(OpCodes.Callvirt, Arguments_GetEnumeratorMI, null);
      generator.Emit(OpCodes.Stloc_2);

      // handle the return value
      if (method.ReturnType != typeof(void)) {
	EmitOut(generator, method.ReturnType, 0);
      }

      for (int parN = 0; parN < pars.Length; parN++) {
	ParameterInfo par = pars[parN];
	if (par.IsOut || par.ParameterType.ToString().EndsWith("&")) {
	  EmitOut(generator, par.ParameterType, parN);
	}
      }

      // Clean up after ourselves
      //generator.EmitWriteLine("methodCall.Dispose()");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_DisposeMI, null);

      //generator.EmitWriteLine("reply.Dispose()");
      generator.Emit(OpCodes.Ldloc_1);
      generator.EmitCall(OpCodes.Callvirt, Message_DisposeMI, null);

      if (method.ReturnType != typeof(void)) {
	generator.Emit(OpCodes.Ldloc_3);
      }
      
      generator.Emit(OpCodes.Ret);

      // Generate the method
      typeB.DefineMethodOverride(methodBuilder, method);
    }

    private void EmitSignalIn(ILGenerator generator, Type parType, int parN, FieldInfo serviceF)
    {
	//generator.EmitWriteLine("enumerator.MoveNext()");
	generator.Emit(OpCodes.Ldloc_0);
	generator.EmitCall(OpCodes.Callvirt, IEnumerator_MoveNextMI, null);
	
	Type outParType = Arguments.MatchType(parType);
	//generator.EmitWriteLine("int selectedIndex = (int) ((DBusType.IDBusType) enumerator.Current).Get(typeof(int))");
	generator.Emit(OpCodes.Pop);
	generator.Emit(OpCodes.Ldloc_0);
	generator.EmitCall(OpCodes.Callvirt, IEnumerator_CurrentMI, null);
	generator.Emit(OpCodes.Castclass, typeof(DBusType.IDBusType));
	generator.Emit(OpCodes.Ldtoken, parType);
	generator.EmitCall(OpCodes.Call, Type_GetTypeFromHandleMI, null);
	generator.EmitCall(OpCodes.Callvirt, IDBusType_GetMI, null);
	// Call the DBusType EmitMarshalOut to make it emit itself
	object[] pars = new object[] {generator, parType, true};
	outParType.InvokeMember("EmitMarshalOut", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, pars, null);
	generator.Emit(OpCodes.Stloc_S, parN);
    }
    

    private void EmitIn(ILGenerator generator, Type parType, int parN, FieldInfo serviceF)
    {
      Type inParType = Arguments.MatchType(parType);
      //generator.EmitWriteLine("methodCall.Arguments.Append(...)");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Message_ArgumentsMI, null);
      generator.Emit(OpCodes.Ldarg_S, parN + 1);

      // Call the DBusType EmitMarshalIn to make it emit itself
      object[] pars = new object[] {generator, parType};
      inParType.InvokeMember("EmitMarshalIn", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, pars, null);

      generator.Emit(OpCodes.Ldsfld, serviceF);
      generator.Emit(OpCodes.Newobj, Arguments.GetDBusTypeConstructor(inParType, parType));
      generator.EmitCall(OpCodes.Callvirt, Arguments_AppendMI, null);
    }

    private void EmitOut(ILGenerator generator, Type parType, int parN)
    {
      Type outParType = Arguments.MatchType(parType);
      //generator.EmitWriteLine("enumerator.MoveNext()");
      generator.Emit(OpCodes.Ldloc_2);
      generator.EmitCall(OpCodes.Callvirt, IEnumerator_MoveNextMI, null);

      //generator.EmitWriteLine("return (" + parType + ") ((DBusType.IDBusType) enumerator.Current).Get(typeof(" + parType + "))");
      generator.Emit(OpCodes.Pop);
      if (parN > 0) {
	generator.Emit(OpCodes.Ldarg_S, parN + 1);
      }
      
      generator.Emit(OpCodes.Ldloc_2);
      generator.EmitCall(OpCodes.Callvirt, IEnumerator_CurrentMI, null);
      generator.Emit(OpCodes.Castclass, typeof(DBusType.IDBusType));
      generator.Emit(OpCodes.Ldtoken, parType);
      generator.EmitCall(OpCodes.Call, Type_GetTypeFromHandleMI, null);
      generator.EmitCall(OpCodes.Callvirt, IDBusType_GetMI, null);

      // Call the DBusType EmitMarshalOut to make it emit itself
      object[] pars = new object[] {generator, parType, parN == 0};
      outParType.InvokeMember("EmitMarshalOut", BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod, null, null, pars, null);
      
      if (parN == 0) {
	generator.Emit(OpCodes.Stloc_3);
      }
    }
    
    public void BuildConstructor(ref TypeBuilder typeB, FieldInfo serviceF, FieldInfo pathF, MethodInfo signalCalledMI, FieldInfo deleF)
    {
      Type[] pars = {typeof(Service), typeof(string)};
      ConstructorBuilder constructor = typeB.DefineConstructor(MethodAttributes.RTSpecialName | 
							       MethodAttributes.Public,
							       CallingConventions.Standard, pars);

      ILGenerator generator = constructor.GetILGenerator();

      LocalBuilder handlerL = generator.DeclareLocal (typeof (Service.SignalCalledHandler));
      handlerL.SetLocalSymInfo ("handler");

      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Call, this.introspector.Constructor);
      //generator.EmitWriteLine("service = myService");
      generator.Emit(OpCodes.Ldarg_1);
      generator.Emit(OpCodes.Stsfld, serviceF);
      //generator.EmitWriteLine("this.pathName = pathName");
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldarg_2);
      generator.Emit(OpCodes.Stfld, pathF);

      //generator.EmitWriteLine("handler = new Service.SignalCalledHandler(Service_SignalCalled)");      
      generator.Emit(OpCodes.Ldarg_1);
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldftn, signalCalledMI);
      generator.Emit(OpCodes.Newobj, Service_SignalCalledHandlerC);
      generator.Emit(OpCodes.Stloc_0);

      //generator.EmitWriteLine("this.delegate_created = handler");
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldloc_0);
      generator.Emit(OpCodes.Stfld, deleF);

      //generator.EmitWriteLine("myService.SignalCalled += handler");
      generator.Emit(OpCodes.Ldloc_0);
      generator.EmitCall(OpCodes.Callvirt, Service_AddSignalCalledMI, null);

      //generator.EmitWriteLine("return");
      generator.Emit(OpCodes.Ret);
    }

    public void BuildSignalConstructor(ref TypeBuilder typeB, FieldInfo serviceF, FieldInfo pathF)
    {
      Type[] pars = {typeof(Service), typeof(string)};
      ConstructorBuilder constructor = typeB.DefineConstructor(MethodAttributes.RTSpecialName | 
							       MethodAttributes.Public,
							       CallingConventions.Standard, pars);

      ILGenerator generator = constructor.GetILGenerator();
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Call, this.introspector.Constructor);
      //generator.EmitWriteLine("service = myService");
      generator.Emit(OpCodes.Ldarg_1);
      generator.Emit(OpCodes.Stsfld, serviceF);
      //generator.EmitWriteLine("this.pathName = pathName");
      generator.Emit(OpCodes.Ldarg_0);
      generator.Emit(OpCodes.Ldarg_2);
      generator.Emit(OpCodes.Stfld, pathF);
      
      //generator.EmitWriteLine("return");
      generator.Emit(OpCodes.Ret);
    }
    
    public void BuildFinalizer (TypeBuilder tb, FieldInfo serviceF, FieldInfo deleF)
    {
       // Note that this is a *HORRIBLE* example of how to build a finalizer
       // It doesn't use the try/finally to chain to Object::Finalize. However,
       // because that is always going to be a nop, lets just ignore that here.
       // If you are trying to find the right code, look at what mcs does ;-).

       MethodBuilder mb = tb.DefineMethod("Finalize",
					  MethodAttributes.Family |
					  MethodAttributes.HideBySig |
					  MethodAttributes.Virtual, 
					  typeof (void), 
					  new Type [0]);
       ILGenerator generator = mb.GetILGenerator();

       //generator.EmitWriteLine("this.service.SignalCalled -= this.delegate_created");
       generator.Emit (OpCodes.Ldarg_0);
       generator.Emit (OpCodes.Ldfld, serviceF);
       generator.Emit (OpCodes.Ldarg_0);
       generator.Emit (OpCodes.Ldfld, deleF);
       generator.EmitCall (OpCodes.Callvirt, Service_RemoveSignalCalledMI, null);
       generator.Emit (OpCodes.Ret);
    }
    
    public object GetSignalProxy()
    {
      Type proxyType = Service.ProxyAssembly.GetType(ObjectName + ".SignalProxy");

      if (proxyType == null) {
	// Build the type
	TypeBuilder typeB = Service.Module.DefineType(ObjectName + ".SignalProxy", 
						      TypeAttributes.Public, 
						      this.type);
	
	FieldBuilder serviceF = typeB.DefineField("service", 
						  typeof(Service), 
						  FieldAttributes.Private | 
						  FieldAttributes.Static);
	FieldBuilder pathF = typeB.DefineField("pathName", 
					       typeof(string), 
					       FieldAttributes.Private);

	BuildSignalConstructor(ref typeB, serviceF, pathF);
	
	// Build the signal handlers
	foreach (DictionaryEntry interfaceEntry in this.introspector.InterfaceProxies) {
	  InterfaceProxy interfaceProxy = (InterfaceProxy) interfaceEntry.Value;
	  foreach (DictionaryEntry signalEntry in interfaceProxy.Signals) {
	    EventInfo eventE = (EventInfo) signalEntry.Value;
	    BuildSignalHandler(eventE, interfaceProxy, ref typeB, serviceF, pathF);
	  }
	}
	
	proxyType = typeB.CreateType();
      
	// Uncomment the following line to produce a DLL of the
	// constructed assembly which can then be examined using
	// monodis. Note that in order for this to work you should copy
	// the client assembly as a dll file so that monodis can pick it
	// up.
	//Service.ProxyAssembly.Save("proxy.dll");
      }

      Type [] parTypes = new Type[] {typeof(Service), typeof(string)};
      object [] pars = new object[] {Service, pathName};
      
      ConstructorInfo constructor = proxyType.GetConstructor(parTypes);
      object instance = constructor.Invoke(pars);
      return instance;
    }
      
    
    public object GetProxy() 
    { 
      Type proxyType = Service.ProxyAssembly.GetType(ObjectName + ".Proxy");
      
      if (proxyType == null) {
	// Build the type
	TypeBuilder typeB = Service.Module.DefineType(ObjectName + ".Proxy", TypeAttributes.Public, this.type);
	
	FieldBuilder serviceF = typeB.DefineField("service", 
						  typeof(Service), 
						  FieldAttributes.Private | 
						  FieldAttributes.Static);
	FieldBuilder pathF = typeB.DefineField("pathName", 
					       typeof(string), 
					       FieldAttributes.Private);
	FieldBuilder deleF = typeB.DefineField("delegate_created", 
					       typeof(Service.SignalCalledHandler), 
					       FieldAttributes.Private);
	BuildFinalizer (typeB, serviceF, deleF);
	
	MethodInfo signalCalledMI = BuildSignalCalled(ref typeB, serviceF, pathF);
	BuildConstructor(ref typeB, serviceF, pathF, signalCalledMI, deleF);
	
	// Build the methods
	foreach (DictionaryEntry interfaceEntry in this.introspector.InterfaceProxies) {
	  InterfaceProxy interfaceProxy = (InterfaceProxy) interfaceEntry.Value;
	  foreach (DictionaryEntry methodEntry in interfaceProxy.Methods) {
	    MethodInfo method = (MethodInfo) methodEntry.Value;
	    BuildMethod(method, interfaceProxy, ref typeB, serviceF, pathF);
	  }
	}
	
	proxyType = typeB.CreateType();
      
	// Uncomment the following line to produce a DLL of the
	// constructed assembly which can then be examined using
	// monodis. Note that in order for this to work you should copy
	// the client assembly as a dll file so that monodis can pick it
	// up.
	//Service.ProxyAssembly.Save(Service.Name + ".proxy.dll");
      }

      Type [] parTypes = new Type[] {typeof(Service), typeof(string)};
      object [] pars = new object[] {Service, pathName};
      
      ConstructorInfo constructor = proxyType.GetConstructor(parTypes);
      object instance = constructor.Invoke(pars);
      return instance;
    }
    
    private Service Service
    {
      get {
	return this.service;
      }
    }

    private string ObjectName
    {
      get {
	return this.introspector.ToString();
      }
    }
  }
}

