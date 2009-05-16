// 
// Tokenizer.cs
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
using System.Text;

namespace Hyena.Json
{
    internal class Tokenizer
    {
        private StreamReader reader;
        private StringBuilder string_buffer;
        
        private char peek = ' ';
        private int current_line = 1;
        private int current_column = 1;
        private int token_start_line;
        private int token_start_column;
        
        public Tokenizer () { Reset (); }
        public Tokenizer (string input) { SetInput (input); }
        public Tokenizer (Stream stream) { SetInput (stream); }
        public Tokenizer (StreamReader reader) { SetInput (reader); }
        
        private void Reset ()
        {
            peek = ' ';
            current_line = 1;
            current_column = 1;
            token_start_line = 0;
            token_start_column = 0;
        }
        
        public void SetInput (StreamReader reader)
        {
            this.reader = reader;
            Reset ();
        }
        
        public void SetInput (Stream stream)
        {
            SetInput (new StreamReader (stream));
        }
        
        public void SetInput (string input)
        {
            SetInput (new MemoryStream (Encoding.UTF8.GetBytes (input)));
        }
        
        private void ReadChar ()
        {
            peek = (char)reader.Read ();
            current_column++;
        }
        
        private void UnexpectedCharacter (char ch)
        {
            throw new ApplicationException (String.Format ("Unexpected character '{0}' at [{1}:{2}]", 
                ch, current_line, current_column - 1));
        }
        
        private void InvalidSyntax (string message)
        {
            throw new ApplicationException (String.Format ("Invalid syntax: {0} at [{1}:{2}]",
                message, current_line, current_column));
        }
        
        private StringBuilder GetStringBuilder ()
        {
            if (string_buffer == null) {
                string_buffer = new StringBuilder (64);
                return string_buffer;
            }
            
            string_buffer.Remove (0, string_buffer.Length);
            return string_buffer;
        }
        
        private string LexString ()
        {
            StringBuilder buffer = GetStringBuilder ();
            bool read = true;
            
            while (!reader.EndOfStream) {
                if (read) {
                    ReadChar ();
                }
                
                read = true;

                if (peek == '\\') {
                    ReadChar ();
                    switch (peek) {
                        case 'u':
                            ReadChar ();
                            buffer.Append ((char)LexInt (true, 4));
                            read = false;
                            break;
                        case '"':
                        case '\\':
                        case '/': buffer.Append (peek); break;
                        case 'b': buffer.Append ('\b'); break;
                        case 'f': buffer.Append ('\f'); break;
                        case 'n': buffer.Append ('\n'); break;
                        case 'r': buffer.Append ('\r'); break;
                        case 't': buffer.Append ('\t'); break;
                        default:
                            UnexpectedCharacter (peek);
                            break;
                    }
                } else if (peek == '"') {
                    ReadChar ();
                    return buffer.ToString ();
                } else {
                    buffer.Append (peek);
                }
            }
            
            if (peek != '"') {
                InvalidSyntax ("Unterminated string, expected '\"' termination, got '" + peek + "'");
            } else if (!read && reader.EndOfStream) {
                ReadChar ();
            }
            
            return buffer.ToString ();
        }
        
        private string LexId ()
        {
            StringBuilder buffer = GetStringBuilder ();

            do {
                buffer.Append (peek);
                ReadChar ();
            } while (Char.IsLetterOrDigit (peek));

            return buffer.ToString ();
        }
        
        private double LexInt ()
        {
            return LexInt (false, 0);
        }
        
        private double LexInt (bool hex, int maxDigits)
        {
            double value = 0.0;
            int count = 0;
            
            do {
                value = (hex ? 16 : 10) * value +  (hex 
                    ? peek >= 'A' && peek <= 'F'
                        ? 10 + peek - 'A'
                        : (peek >= 'a' && peek <= 'f'
                            ? 10 + peek - 'a'
                            : peek - '0')
                    : peek - '0');
                    
                if (maxDigits > 0 && ++count >= maxDigits) {
                    ReadChar ();
                    return value;
                }
                
                ReadChar ();
            } while (Char.IsDigit (peek) || (hex && ((peek >= 'a' && peek <= 'f') || (peek >= 'A' && peek <= 'F'))));
            
            return value;
        }
        
        private double LexFraction ()
        {
            double fraction = 0;
            double d = 10;
            
            while (true) {
                ReadChar ();

                if (!Char.IsDigit (peek)) {
                    break;
                }

                fraction += (peek - '0') / d;
                d *= 10;
            }
            
            return fraction;
        }
        
        private double LexNumber ()
        {
            double value = 0.0;
            bool negate = peek == '-';
            if (negate) {
                ReadChar ();
            }
            
            if (peek != '0') {
                value = LexInt ();
            } else {
                ReadChar ();
            }
            
            if (peek == '.') {
                value += LexFraction ();
            }
            
            if (peek == 'e' || peek == 'E') {
                ReadChar ();
                if (peek == '-') {
                    ReadChar ();
                    value /= Math.Pow (10, LexInt ());
                } else if (peek == '+') {
                    ReadChar ();
                    value *= Math.Pow (10, LexInt ());
                } else if (Char.IsDigit (peek)) {
                    value *= Math.Pow (10, LexInt ());
                } else {
                    InvalidSyntax ("Malformed exponent");
                }
            }
            
            if (Char.IsDigit (peek)) {
                InvalidSyntax ("Numbers starting with 0 must be followed by a . or not " +
                    "followed by a digit (octal syntax not legal)");
            }
            
            return negate ? -1.0 * value : value;
        }
        
        public Token Scan ()
        {
            Token token = InnerScan ();
            if (token == null) {
                return null;
            }
            
            token.SourceLine = token_start_line;
            token.SourceColumn = token_start_column - 1;
            return token;
        }

        private Token InnerScan ()
        {
            for (; ; ReadChar ()) {
                if (Char.IsWhiteSpace (peek) && peek != '\n') {
                    continue;
                } else if (peek == '\n') {
                    current_line++;
                    current_column = 0;
                } else {
                    break;
                }
            }

            token_start_column = current_column;
            token_start_line = current_line;

            switch (peek) {
                case '{': ReadChar (); return new Token (TokenType.ObjectStart);
                case '}': ReadChar (); return new Token (TokenType.ObjectFinish);
                case '[': ReadChar (); return new Token (TokenType.ArrayStart);
                case ']': ReadChar (); return new Token (TokenType.ArrayFinish);
                case ',': ReadChar (); return new Token (TokenType.Comma);
                case ':': ReadChar (); return new Token (TokenType.Colon);
                case '"': return new Token (TokenType.String, LexString ());
                default:
                    if (peek == '-' || Char.IsDigit (peek)) {
                        return new Token (TokenType.Number, LexNumber ());
                    } else if (Char.IsLetter (peek)) {
                        string identifier = LexId ();
                        switch (identifier) {
                            case "true": return new Token (TokenType.Boolean, true);
                            case "false": return new Token (TokenType.Boolean, false);
                            case "null": return new Token (TokenType.Null);
                            default:
                                InvalidSyntax ("Invalid identifier '" + identifier + "'");
                                break;
                        }
                    }
                
                    if (peek != Char.MaxValue) {
                        UnexpectedCharacter (peek);
                    }
                    break;
            }
            
            return null;
        }
    }
}
