using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{

    public enum TokenType
    {
        Invalid,
        Integer,
        String,
        Word,
        ParenList,
        OpenBlock,
        CloseBlock,
        Pair,
        ListDelimeter
    }

    public class Tokenizer
    {
        
        public static TokenEnumerator Tokenize(String input)
        {
            List<Token> tokens = new List<Token>();
            int currentLine = 1;
            Boolean passToken = false;
            Boolean inComment = false;
            Boolean inBlockComment = false;
            Boolean inString = false;
            Boolean inParenList = false;
            Boolean inPair = false;
            Object pairPart = null;
            List<Object> currentParenList = null;
            int tokenStartLine = 0;
            char stringOpener = '\'';
            String currToken = "";
            for (int i=0; i<input.Length; i++)
            {
                currToken += input[i];
                if (input[i] == '\n') currentLine++;
                if (inBlockComment)
                {
                    if (currToken.EndsWith("*/"))
                    {
                        inBlockComment = false;
                        //Console.WriteLine("Found Block Comment:\n" + currToken + "\n");
                        currToken = "";
                    }
                    else continue;
                }
                else if (inComment)
                {
                    if (currToken.EndsWith("\n"))
                    {
                        inComment = false;
                        //Console.WriteLine("Found Comment: " + currToken);
                        currToken = "";
                    }
                    else continue;
                }
                else if (inString)
                {
                    if (currToken.EndsWith(stringOpener.ToString()))
                    {
                        inString = false;
                        tokens.Add(new Token { TokenType = TokenType.String, Line = tokenStartLine, Value = currToken.Substring(0, currToken.Length - 1) });
                        currToken = "";
                    }
                    else continue;
                }
                else if (inParenList)
                {
                    if (currToken.EndsWith(","))
                    {
                        currToken = currToken.Substring(0, currToken.Length - 1).Trim();
                        Object castToken = AutoCastInt(currToken, currentLine, out TokenType type);
                        currentParenList.Add(castToken);
                        currToken = "";
                    }
                    else if (currToken.EndsWith(")"))
                    {
                        currToken = currToken.Substring(0, currToken.Length - 1).Trim();
                        Object castToken = AutoCastInt(currToken, currentLine, out TokenType type);
                        currentParenList.Add(castToken);
                        currToken = "";
                        tokens.Add(new Token { TokenType = TokenType.ParenList, Line = tokenStartLine, Value = currentParenList });
                        inParenList = false;
                    }
                }
                else if (inPair)
                {
                    if (currToken.Trim().Length > 0 && (char.IsWhiteSpace(currToken[currToken.Length - 1]) || currToken.EndsWith(",")))
                    {
                        inPair = false;
                        currToken = currToken.Trim();
                        Object otherPairPart = AutoCastInt(currToken.TrimEnd(new char[] { ',' }), currentLine, out TokenType secondType);
                        tokens.Add(new Token { Line = tokenStartLine, TokenType = TokenType.Pair, Value = new Object[] { pairPart, otherPairPart } });
                        if (currToken.EndsWith(",")) tokens.Add(new Token { Line = currentLine, TokenType = TokenType.ListDelimeter, Value = "," });
                        currToken = "";
                    }
                }
                else if (currToken.EndsWith("\"") || currToken.EndsWith("\'"))
                {
                    inString = true;
                    stringOpener = currToken[currToken.Length - 1];
                    tokenStartLine = currentLine;
                    currToken = currToken.Substring(0, currToken.Length - 1);
                    passToken = true;
                }
                else if (currToken.EndsWith("/*"))
                {
                    inBlockComment = true;
                    currToken = currToken.Substring(0, currToken.Length - 2) + " ";
                    passToken = true;
                }
                else if (currToken.EndsWith("#"))
                {
                    inComment = true;
                    currToken = currToken.Substring(0, currToken.Length - 1) + " ";
                    passToken = true;
                }
                else if (currToken.EndsWith("("))
                {
                    inParenList = true;
                    currentParenList = new List<object>();
                    tokenStartLine = currentLine;
                    currToken = currToken.Substring(0, currToken.Length - 1) + " ";
                    passToken = true;
                }
                else if (currToken.Equals("{"))
                {
                    tokens.Add(new Token { Line = currentLine, TokenType = TokenType.OpenBlock, Value = "{" });
                    currToken = "";
                }
                else if (currToken.Equals("}"))
                {
                    tokens.Add(new Token { Line = currentLine, TokenType = TokenType.CloseBlock, Value = "}" });
                    currToken = "";
                }
                else if (currToken.EndsWith("{"))
                {
                    currToken = currToken.Substring(0, currToken.Length - 1).Trim();
                    if (currToken.Length > 0) tokens.Add(new Token { Line = currentLine, Value = AutoCastInt(currToken, currentLine, out TokenType type), TokenType = type });
                    currToken = "";
                    tokens.Add(new Token { Line = currentLine, TokenType = TokenType.OpenBlock, Value = "{" });
                    continue;
                }
                else if (currToken.EndsWith("}"))
                {
                    currToken = currToken.Substring(0, currToken.Length - 1).Trim();
                    if (currToken.Length > 0) tokens.Add(new Token { Line = currentLine, Value = AutoCastInt(currToken, currentLine, out TokenType type), TokenType = type });
                    currToken = "";
                    tokens.Add(new Token { Line = currentLine, TokenType = TokenType.CloseBlock, Value = "}" });
                    continue;
                }
                else if (currToken.EndsWith(":") && !currToken.StartsWith(":") && !currToken.Contains("["))
                {
                    inPair = true;
                    tokenStartLine = currentLine;
                    pairPart = AutoCastInt(currToken.Substring(0, currToken.Length - 1), currentLine, out TokenType type);
                    currToken = "";
                }
                else if (currToken.Equals(","))
                {
                    currToken = "";
                    tokens.Add(new Token { Line = currentLine, TokenType = TokenType.ListDelimeter, Value = "," });
                }
                else if (currToken.EndsWith(","))
                {
                    currToken = currToken.Substring(0, currToken.Length - 1).Trim();
                    if (currToken.Length > 0) tokens.Add(new Token { Line = currentLine, Value = AutoCastInt(currToken, currentLine, out TokenType type), TokenType = type });
                    currToken = "";
                    tokens.Add(new Token { Line = currentLine, TokenType = TokenType.ListDelimeter, Value = "," });
                    continue;
                }
                if (passToken || ((currToken.Length > 0 && char.IsWhiteSpace(currToken[currToken.Length - 1])) && !inString && !inComment && !inBlockComment && !inParenList) || (!inComment && !inBlockComment && !inString && !inParenList && i == input.Length - 1))
                {
                    passToken = false;
                    currToken = currToken.Trim();
                    if (currToken.Length > 0)
                    {
                        Object castToken = AutoCastInt(currToken, currentLine, out TokenType type);
                        tokens.Add(new Token { TokenType = type, Line = currentLine, Value = castToken });
                        currToken = "";
                    }
                }
            }
            if (inString)
            {
                throw new TokenizerException("Unfinished string on line " + tokenStartLine);
            }
            if (inParenList)
            {
                throw new TokenizerException("Unfinished parameter/argument list on line " + tokenStartLine);
            }
            return new TokenEnumerator(tokens);
        }

        public static Object AutoCastInt(String token, int line, out TokenType type) //Try to cast a string to an integer in various formats.  If not successful, returns the original string
        {
            long intToken = 0;
            SymbolSelector sel;
            if ((sel = SymbolSelector.TryParse(token)) != null)
            {
                Object selCast = AutoCastInt(sel.Symbol, line, out TokenType selType); //Probably will never cause infinite recursion
                if (selType == TokenType.Integer)
                {
                    type = TokenType.Integer;
                    return ((long)selCast & sel.Selector.ToLongMask()) >> sel.Selector.LowerBound;
                }
                else
                {
                    type = TokenType.Word;
                    return token;
                }
            }
            else if (token.StartsWith("0x"))
            {
                token = token.Substring(2, token.Length - 2);
                if (!long.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out intToken))
                {
                    throw new TokenizerException("Malformed hexadecimal number at line " + line);
                }
                type = TokenType.Integer;
                return intToken;
            }
            else if (long.TryParse(token, out intToken))
            {
                type = TokenType.Integer;
                return intToken;
            }
            else
            {
                type = TokenType.Word;
                return token;
            }
        }

    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class Token
    {
        public TokenType TokenType { get; set; }
        public object Value { get; set; }
        public int Line { get; set; }

        public String DebuggerDisplay
        {
            get => $"{{Type = {TokenType.ToString()}}} {{Value = {((Value.GetType().IsArray) ? (Value as Object[]).ToListString() : Value.ToString())}}}";
        }
    }

    public class TokenEnumerator
    {
        List<Token> Tokens;
        int index;
        public Token Next { get; private set; }
        public Token Current { get; private set; }
        public Token Last { get; private set; }

        public TokenEnumerator(List<Token> Tokens)
        {
            this.Tokens = Tokens;
            this.index = 0;
            Last = null;
            Current = (Tokens.Count > 0) ? Tokens[0] : null;
            Next = (Tokens.Count > 1) ? Tokens[1] : null;
        }

        public Boolean HasNext() => Next != null;

        public Boolean HasToken() => Current != null;

        public void Advance()
        {
            index++;
            Last = Current;
            if (Tokens.Count > index)
            {
                Current = Tokens[index];
                Next = (Tokens.Count > index + 1) ? Tokens[index + 1] : null;
            }
            else
            {
                Current = null;
                Next = null;
            }
        }

    }

    public class TokenizerException : Exception
    {
        public TokenizerException(String message) : base(message) { }
    }

}
