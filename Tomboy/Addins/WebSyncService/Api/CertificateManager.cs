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
// Copyright (c) 2009 Novell, Inc. (http://www.novell.com)
// Copyright (c) 2009 Canonical, Ltd (http://www.canonical.com)
// 
// Authors: 
//      Rodrigo Moya <rodrigo.moya@canonical.com>
// 

using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Tomboy.WebSync.Api
{
	public class CertificateManager : ICertificatePolicy
	{

		public bool CheckValidationResult (ServicePoint sp, 
						   X509Certificate certificate,
						   WebRequest request,
						   int error)

		{
			if (error == 0)
				return true;
 
			/* FIXME: can't open a dialog, since this is called on a thread
			Gtk.MessageDialog dialog = new Gtk.MessageDialog (
				null, 0,
				Gtk.MessageType.Error,
				Gtk.ButtonsType.YesNo,
				"Certificate from web server not known. Do you want to accept it?");
			int response = dialog.Run ();
			dialog.Destroy ();

			return (response == (int) Gtk.ResponseType.Yes); */

			return true;
		}
	}
}
