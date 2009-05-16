//
// TokenizerTests.cs
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

#if ENABLE_TESTS

using System;
using System.Reflection;
using NUnit.Framework;

using Hyena.Json;

namespace Hyena.Json.Tests
{
    [TestFixture]
    public class TokenizerTests : Hyena.Tests.TestBase
    {
        private Tokenizer tokenizer;
        
        [TestFixtureSetUp]
        public void Setup ()
        {
            tokenizer = new Tokenizer ();
        }
    
        [Test]
        public void Whitespace ()
        {
            AssertTokenStream ("");
            AssertTokenStream (" ");
            AssertTokenStream ("\f\n\r\t ");
        }
        
        [Test]
        public void BoolNull ()
        {
            // Boolean/null tests
            AssertTokenStream ("true", Token.Bool (true));
            AssertTokenStream ("false", Token.Bool (false));
            AssertTokenStream ("null", Token.Null);
        }
        
        [Test]
        public void NumberInt ()
        {
            // Number tests
            AssertTokenStream ("0", Token.Number (0));
            AssertTokenStream ("-0", Token.Number (-0));
            
            AssertTokenStream ("9", Token.Number (9));
            AssertTokenStream ("-9", Token.Number (-9));
            
            AssertTokenStream ("14", Token.Number (14));
            AssertTokenStream ("-14", Token.Number (-14));
            
            AssertTokenStream ("15309", Token.Number (15309));
            AssertTokenStream ("-15309", Token.Number (-15309));
        }
        
        [Test]
        public void NumberFloat ()
        {
            AssertTokenStream ("0.0", Token.Number (0.0));
            AssertTokenStream ("-0.0", Token.Number (-0.0));
            
            AssertTokenStream ("1.9", Token.Number (1.9));
            AssertTokenStream ("-1.9", Token.Number (-1.9));
            
            AssertTokenStream ("9.1", Token.Number (9.1));
            AssertTokenStream ("-9.1", Token.Number (-9.1));
            
            AssertTokenStream ("15309.0", Token.Number (15309.0));
            AssertTokenStream ("15309.9", Token.Number (15309.9));
            AssertTokenStream ("-15309.01", Token.Number (-15309.01));
            AssertTokenStream ("-15309.9009", Token.Number (-15309.9009));
        }
        
        [Test]
        public void NumberExponent ()
        {
            AssertTokenStream ("20.6e3", Token.Number (20.6e3));
            AssertTokenStream ("20.6e+3", Token.Number (20.6e+3));
            AssertTokenStream ("20.6e-3", Token.Number (20.6e-3));
            AssertTokenStream ("-20.6e3", Token.Number (-20.6e3));
            AssertTokenStream ("-20.6e+3", Token.Number (-20.6e+3));
            AssertTokenStream ("-20.6e-3", Token.Number (-20.6e-3));
            
            AssertTokenStream ("1e1", Token.Number (1e1));
            AssertTokenStream ("1E2", Token.Number (1E2));
            AssertTokenStream ("1.0e1", Token.Number (1.0e1));
            AssertTokenStream ("1.0E1", Token.Number (1.0E1));
        }
        
        [Test]
        public void Strings ()
        {
            AssertTokenStream (@"""""", Token.String (""));
            AssertTokenStream (@"""a""", Token.String ("a"));
            AssertTokenStream (@"""ab""", Token.String ("ab"));
            AssertTokenStream (@" ""a b"" ", Token.String ("a b"));
            AssertTokenStream (@"""\""\""""", Token.String ("\"\""));
            AssertTokenStream (@" ""a \"" \"" b"" ", Token.String ("a \" \" b"));
            AssertTokenStream (@"""\ubeef""", Token.String ("\ubeef"));
            AssertTokenStream (@"""\u00a9""", Token.String ("\u00a9"));
            AssertTokenStream (@"""\u0000\u0001\u0002""", Token.String ("\u0000\u0001\u0002"));
            AssertTokenStream (@"""1\uabcdef0""", Token.String ("1\uabcdef0"));
            AssertTokenStream (@"""\b\f\n\r\t""", Token.String ("\b\f\n\r\t"));
        }
        
        [Test]
        public void Container ()
        {
            AssertTokenStream ("{}", Token.ObjectStart, Token.ObjectFinish);
            AssertTokenStream ("[]", Token.ArrayStart, Token.ArrayFinish);
            AssertTokenStream ("{  }", Token.ObjectStart, Token.ObjectFinish);
            AssertTokenStream ("[  ]", Token.ArrayStart, Token.ArrayFinish);
            AssertTokenStream ("[{}]", Token.ArrayStart, Token.ObjectStart, Token.ObjectFinish, Token.ArrayFinish);
            AssertTokenStream ("[[[ { } ]]]", 
                Token.ArrayStart, Token.ArrayStart, Token.ArrayStart, 
                Token.ObjectStart, Token.ObjectFinish, 
                Token.ArrayFinish, Token.ArrayFinish, Token.ArrayFinish);
        }
        
        [Test]
        public void Array ()
        {
            AssertTokenStream ("[1]", Token.ArrayStart, Token.Number (1), Token.ArrayFinish);
            AssertTokenStream ("[1,0]", Token.ArrayStart, Token.Number (1), Token.Comma, Token.Number (0), Token.ArrayFinish);
            AssertTokenStream ("[\"a\",true,null]", Token.ArrayStart, Token.String ("a"), Token.Comma, 
                Token.Bool (true), Token.Comma, Token.Null, Token.ArrayFinish);
            AssertTokenStream ("[0,1,[[2,[4]],5],6]", Token.ArrayStart, Token.Number (0), Token.Comma, Token.Number (1),
                 Token.Comma, Token.ArrayStart, Token.ArrayStart, Token.Number (2), Token.Comma, Token.ArrayStart, 
                 Token.Number (4), Token.ArrayFinish, Token.ArrayFinish, Token.Comma, Token.Number (5), Token.ArrayFinish,
                 Token.Comma, Token.Number (6), Token.ArrayFinish);
        }
        
        [Test]
        public void Object ()
        {
            AssertTokenStream ("{\"a\":{}}", Token.ObjectStart, Token.String ("a"), Token.Colon, Token.ObjectStart, 
                Token.ObjectFinish, Token.ObjectFinish);
            AssertTokenStream ("{\"a\":{\"b\":[],\"c\":false}}", Token.ObjectStart, Token.String ("a"), 
                Token.Colon, Token.ObjectStart, Token.String ("b"), Token.Colon, Token.ArrayStart, Token.ArrayFinish, 
                Token.Comma, Token.String ("c"), Token.Colon, Token.Bool (false), Token.ObjectFinish, Token.ObjectFinish);
            AssertTokenStream ("[{\"a\":{},{}]", Token.ArrayStart, Token.ObjectStart, Token.String ("a"), Token.Colon, 
                Token.ObjectStart, Token.ObjectFinish, Token.Comma, Token.ObjectStart, Token.ObjectFinish, Token.ArrayFinish);
        }    
        
        private void AssertTokenStream (string input, params Token [] tokens)
        {
            int cmp_idx = 0;
            tokenizer.SetInput (input);
            
            while (true) {
                Token token = tokenizer.Scan ();
                if (token == null) {
                    if (cmp_idx != tokens.Length) {
                        throw new ApplicationException ("Unexpected EOF");
                    }
                    break;
                }
                
                Token compare = tokens[cmp_idx++];
                if (compare.Type != token.Type) {
                    throw new ApplicationException (String.Format ("TokenTypes do not match (exp {0}, got {1}", 
                        compare.Type, token.Type));
                }
                
                if (compare.Value == null && token.Value == null) {
                    continue;
                }
                
                if ((compare.Type == TokenType.Number && (double)compare.Value != (double)token.Value) ||
                    (compare.Type == TokenType.String && (string)compare.Value != (string)token.Value) ||
                    (compare.Type == TokenType.Boolean && (bool)compare.Value != (bool)token.Value)) {
                    throw new ApplicationException ("Token values do not match");
                }
            }
        }
    }
}

#endif
