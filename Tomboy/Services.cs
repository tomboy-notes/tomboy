// Permission is hereby granted, free of charge, to any person obtaining 
// a copy of this software and associated documentation files (the 
// "Software"), to deal in the Software without restriction, including 
// without limitation the rights to use, copy, modify, merge, publish, 
// distribute, sublicense, and/or sell copies of the Software, and to 
// permit persons to whom the Software is furnished to do so, subject to 
// the following conditions: 
//  
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software. 
//  
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION 
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE. 
// 
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com) 
// 
// Authors: 
//      Sandy Armstrong <sanfordarmstrong@gmail.com>
// 


using System;

namespace Tomboy
{
	public class Services
	{
		private static IPreferencesClient prefs;
		private static INativeApplication nativeApp;
		private static IKeybinder keybinder;
		private static IPlatformFactory factory;
		
		static Services ()
		{
#if WIN32
			factory = new WindowsFactory ();
#elif MAC
			factory = new MacFactory ();
#else
			factory = new GnomeFactory ();
#endif
			
			nativeApp = factory.CreateNativeApplication ();
		}

		public static IPlatformFactory Factory {
			get {
				return factory;
			}
		}

		public static IPreferencesClient Preferences {
			get {
				if (prefs == null)
					prefs = factory.CreatePreferencesClient ();
				return prefs;
			}
		}

		public static INativeApplication NativeApplication {
			get {
				return nativeApp;
			}
		}

		public static IKeybinder Keybinder {
			get {
				// Initialize on-demand. XKeybinder must not be
				// created too early.
//				if (keybinder == null)
//					keybinder = factory.CreateKeybinder ();
//				return keybinder;
				//FIXME: We no longer have keybinders, need to make a new one.
				return new NullKeybinder ();
			}
		}
	}
}