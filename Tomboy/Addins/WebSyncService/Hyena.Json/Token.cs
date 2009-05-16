// 
// Token.cs
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

namespace Hyena.Json
{   
    internal class Token
    {
        public Token (TokenType type) : this (type, null)
        {
        }
        
        public Token (TokenType type, object value)
        {
            this.type = type;
            this.value = value;
        }
        
        private TokenType type;
        public TokenType Type {
            get { return type; }
        }

        private object value;
        public object Value {
            get { return value; }
            set { this.value = value; }
        }

        private int source_line;
        public int SourceLine {
            get { return source_line; }
            internal set { source_line = value; }
        }
        
        private int source_column;
        public int SourceColumn {
            get { return source_column; }
            internal set { source_column = value; }
        }
        
        internal static Token ObjectStart {
            get { return new Token (TokenType.ObjectStart); }
        }
        
        internal static Token ObjectFinish {
            get { return new Token (TokenType.ObjectFinish); }
        }
        
        internal static Token ArrayStart {
            get { return new Token (TokenType.ArrayStart); }
        }
        
        internal static Token ArrayFinish {
            get { return new Token (TokenType.ArrayFinish); }
        }
        
        internal static Token Null {
            get { return new Token (TokenType.Null); }
        }
        
        internal static Token Comma {
            get { return new Token (TokenType.Comma); }
        }
        
        internal static Token Colon {
            get { return new Token (TokenType.Colon); }
        }
        
        internal static Token Number (double value)
        {
            return new Token (TokenType.Number, value);
        }
        
        internal static Token String (string value)
        {
            return new Token (TokenType.String, value);
        }
        
        internal static Token Bool (bool value)
        {
            return new Token (TokenType.Boolean, value);
        }
    }
}
