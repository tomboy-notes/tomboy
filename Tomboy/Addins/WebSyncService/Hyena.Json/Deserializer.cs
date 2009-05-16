// 
// Deserializer.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2008 Novell, Inc.
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

namespace Hyena.Json
{
    public class Deserializer
    {
        private Tokenizer tokenizer = new Tokenizer ();
        
        public Deserializer () { }
        public Deserializer (string input) { SetInput (input); }
        public Deserializer (Stream stream) { SetInput (stream); }
        public Deserializer (StreamReader reader) { SetInput (reader); }
        
        public Deserializer SetInput (StreamReader reader)
        {
            tokenizer.SetInput (reader);
            return this;
        }
        
        public Deserializer SetInput (Stream stream)
        {
            tokenizer.SetInput (stream);
            return this;
        }
        
        public Deserializer SetInput (string input)
        {
            tokenizer.SetInput (input);
            return this;
        }
        
        public object Deserialize ()
        {
            Token token = CheckScan (TokenType.Value, true);
            if (token == null) {
                return null;
            }
            
            return Parse (token);
        }
        
        private object Parse (Token token)
        {
            if (token.Type == TokenType.ObjectStart) {
                return ParseObject ();
            } else if (token.Type == TokenType.ArrayStart) {
                return ParseArray ();
            }
            
            return token.Value;
        }
        
        private JsonObject ParseObject ()
        {
            JsonObject obj = new JsonObject ();
            
            while (true) {
                Token key = CheckScan (TokenType.String | TokenType.ObjectFinish);
                if (key.Type == TokenType.ObjectFinish) {
                    break;
                }
                
                CheckScan (TokenType.Colon);
                Token value = CheckScan (TokenType.Value);
                
                object value_val = value.Value;
                if (value.Type == TokenType.ArrayStart) {
                    value_val = ParseArray ();
                } else if (value.Type == TokenType.ObjectStart) {
                    value_val = ParseObject ();
                }
                
                obj.Add ((string)key.Value, value_val);
                
                Token token = CheckScan (TokenType.Comma | TokenType.ObjectFinish);
                if (token.Type == TokenType.ObjectFinish) {
                     break;
                }
            }
            
            return obj;
        }
        
        private JsonArray ParseArray ()
        {
            JsonArray array = new JsonArray ();
            
            while (true) {
                Token value = CheckScan (TokenType.Value | TokenType.ArrayFinish);
                if (value.Type == TokenType.ArrayFinish) {
                    break;
                }
                
                array.Add (Parse (value));
                
                Token token = CheckScan (TokenType.Comma | TokenType.ArrayFinish);
                if (token.Type == TokenType.ArrayFinish) {
                     break;
                }
            }
            
            return array;
        }
        
        private Token CheckScan (TokenType expected)
        {
            return CheckScan (expected, false);
        }
        
        private Token CheckScan (TokenType expected, bool eofok)
        {
            Token token = tokenizer.Scan ();
            if (token == null && eofok) {
                return null;
            } else if (token == null) {
                UnexpectedEof (expected);
            } else if ((expected & token.Type) == 0) {
                UnexpectedToken (expected, token);
            }
            return token;
        }
        
        private void UnexpectedToken (TokenType expected, Token got)
        {
            throw new ApplicationException (String.Format ("Unexpected token {0} at [{1}:{2}]; expected {3}", 
                got.Type, got.SourceLine, got.SourceColumn, expected));
        }
        
        private void UnexpectedEof (TokenType expected)
        {
            throw new ApplicationException (String.Format ("Unexpected End of File; expected {0}", expected));
        }
    }
}
