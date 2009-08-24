//
// XdgBaseDirectorySpec.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2006-2008 Novell, Inc.
//
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

using System;
using System.IO;

namespace Hyena
{
    public static class XdgBaseDirectorySpec
    {
        public static string GetUserDirectory (string key, string fallback)
        {
            string home_dir = Environment.GetFolderPath (Environment.SpecialFolder.Personal);
            string config_dir = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
            
            string env_path = Environment.GetEnvironmentVariable (key);
            if (!String.IsNullOrEmpty (env_path)) {
                return env_path;
            }

            string user_dirs_path = Path.Combine (config_dir, "user-dirs.dirs");

            if (!File.Exists (user_dirs_path)) {
                return Path.Combine (home_dir, fallback);
            }

            try {
                using (StreamReader reader = new StreamReader (user_dirs_path)) {
                    string line;
                    while ((line = reader.ReadLine ()) != null) {
                        line = line.Trim ();
                        int delim_index = line.IndexOf ('=');
                        if (delim_index > 8 && line.Substring (0, delim_index) == key) {
                            string path = line.Substring (delim_index + 1).Trim ('"');
                            bool relative = false;

                            if (path.StartsWith ("$HOME/")) {
                                relative = true;
                                path = path.Substring (6);
                            } else if (path.StartsWith ("~")) {
                                relative = true;
                                path = path.Substring (1);
                            } else if (!path.StartsWith ("/")) {
                                relative = true;
                            }

                            return relative ? Path.Combine (home_dir, path) : path;
                        }
                    }
                }
            } catch (FileNotFoundException) {
            }
            
            return Path.Combine (home_dir, fallback);
        }
    }
}
