using System;
using System.Linq;
using System.Collections.Generic;

namespace Microassembler
{

    public class MicroprogramParser //Used to parse an input file into the first stage of a microprogram.  The result needs to be passed through the Linker
    {
        private Microprogram Microprogram;
        private TokenEnumerator Enumerator;

        public Microprogram ParseProgram(String program)
        {
            Microprogram = new Microprogram();
            Enumerator = Tokenizer.Tokenize(program);
            if (Enumerator == null) return null;
            while (Enumerator.HasToken())
            {
                Token token = Enumerator.Current;
                Enumerator.Advance();
                if (token.TokenType == TokenType.Word)
                {
                    String keyword = (String)token.Value;
                    switch (keyword.ToLower())
                    {
                        case "config":
                            if (!ProcessConfigs()) return null;
                            break;
                        case "const":
                            if (!ProcessConstant()) return null;
                            break;
                        case "control":
                            if (!ProcessControlWordLabel()) return null;
                            break;
                        case "sequence":
                            if (!ProcessSequence()) return null;
                            break;
                        default:
                            throw new MicroassemblerParseException(token, "Keyword expected");
                    }
                }
                else
                {
                    throw new MicroassemblerParseException(token, "Keyword expected");
                }
            }
            return Microprogram;
        }

        public Boolean ProcessSequence() //Processes a normal sequence
        {
            String symbol = GetWordToken();
            Sequence sequence = ParseSequence(false);
            CheckSymbolWarning(symbol, Enumerator.Last.Line);
            Microprogram[symbol] = sequence;
            Console.WriteLine($"Processed sequence '{symbol}'");
            return true;
        }

        public Sequence ParseSequence(Boolean isMacro) // Parses a sequence and return an object representation (used for both normal sequences and macros)
        {
            Sequence sequence = new Sequence();
            if (Enumerator.HasToken() && Enumerator.Current.TokenType == TokenType.ParenList)
            {
                if (!isMacro) throw new MicroassemblerParseException(Enumerator.Last, "Only macros can define a parameter list");
                List<Object> parameters = (List<Object>)Enumerator.Current.Value;
                Enumerator.Advance();
                if (parameters.Where(p => !(p is String) || (p is String && ((String)p).Any(Char.IsWhiteSpace))).Any())
                {
                    throw new MicroassemblerParseException(Enumerator.Last, "Parameter definitions may only contain non-whitespace-delimited words");
                }
                sequence.Parameters.AddRange(parameters.Cast<String>());
            }
            VerifySyntaxToken(TokenType.OpenBlock, "{");
            String word = "";
            do
            {
                word = GetWordToken();
                if (word.StartsWith("::") && word.EndsWith("::"))
                {
                    String label = word.Substring(2, word.Length - 4);
                    sequence[label] = new SequenceLabel { PostExpansionAddress = sequence.Steps.Count };
                }
                else if (Enumerator.HasToken() && Enumerator.Current.TokenType == TokenType.ParenList)
                {
                    List<Object> arguments = (List<Object>)Enumerator.Current.Value;
                    Enumerator.Advance();
                    SequenceMacroReference step = new SequenceMacroReference() { Arguments = arguments, Symbol = word };
                    sequence.Steps.Add(step);
                }
                else if (word.ToLower().Equals("assert"))
                {
                    SequenceAssertion step = ParseSequenceAssertion();
                    sequence.Steps.Add(step);
                }
                else
                {
                    throw new MicroassemblerParseException(Enumerator.Last, "Unexpected token");
                }
            }
            while (!word.Equals("}"));

            return sequence;
        }

        public SequenceAssertion ParseSequenceAssertion() //Parses an assertion statement 
        {
            SequenceAssertion assertion = new SequenceAssertion(Microprogram.ControlWordWidth);
            VerifySyntaxToken(TokenType.OpenBlock, "{");
            String word;
            do
            {
                word = GetWordToken();



            }
            while (!word.Equals("}"));

            return assertion;
        }

        public bool ProcessConstant() // Processes a constant and add it to the top-level symbol table
        {
            String key = GetWordToken();
            int value = GetIntToken();
            CheckSymbolWarning(key, Enumerator.Last.Line);
            Microprogram.Symbols[key] = value;
            return true;
        }

        public bool ProcessControlWordLabel() //Processes a control word label and adds it to the list of control word labels
        {
            String name = GetWordToken();
            int bank = GetIntToken();
            int msb = GetIntToken();
            int lsb = (Enumerator.HasToken() && Enumerator.Current.TokenType == TokenType.Integer) ? GetIntToken() : msb;
            BitMask mask = new BitMask(msb, lsb);
            Microprogram.ControlWordLabels.Add(name, new ControlWordLabel { Bank = bank, Name = name, Mask = mask });
            Console.WriteLine($"Added new control word label '{name}' on bank {bank} at {mask}");
            return true;
        }

        public bool ProcessConfigs() // Processes the config portion of the microprogram
        {
            VerifySyntaxToken(TokenType.OpenBlock, "{");
            while (Enumerator.HasToken())
            {
                Token token = Enumerator.Current;
                Enumerator.Advance();
                if (token.TokenType == TokenType.CloseBlock && token.Value.Equals("}")) break;
                if (token.TokenType != TokenType.Word)
                {
                    throw new MicroassemblerParseException(token, "Config name expected");
                }
                String configName = (String)token.Value;
                int arg;
                int arg2;
                switch (configName.ToLower())
                {
                    case "controlwordwidth":
                        arg = GetIntToken();
                        Microprogram.ControlWordWidth = arg;
                        Console.WriteLine($"Set control word width to {arg}");
                        break;
                    case "microprogramlength":
                        arg = GetIntToken();
                        Microprogram.MicroprogramLength = arg;
                        Console.WriteLine($"Set microprogram length to {arg}");
                        break;
                    case "opwidth":
                        arg = GetIntToken();
                        Microprogram.OpcodeWidth = arg;
                        Console.WriteLine($"Set opcode width to {arg}");
                        break;
                    case "bankmask":
                        arg = GetIntToken();
                        arg2 = GetIntToken();
                        Microprogram.BankSelectorMask = new BitMask(arg, arg2);
                        Console.WriteLine($"Set control word bank select mask to {Microprogram.BankSelectorMask}");
                        break;
                    default:
                        throw new MicroassemblerParseException(token, "Config name expected");
                }
            }
            return true;
        }

        public void CheckSymbolWarning(String symbol) //Checks for the existance of a symbol and prints a warning if it exists
        {
            if (Microprogram[symbol] != null)
            {
                Console.WriteLine($"Duplicate symbol {symbol}, previous definition will be overridden");
            }
        }

        public void CheckSymbolWarning(String symbol, int line) //Checks for the existance of a symbol and prints a warning if it exists
        {
            if (Microprogram[symbol] != null)
            {
                Console.WriteLine($"Duplicate symbol {symbol} defined on line {line}, previous definition will be overridden");
            }
        }

        public int GetIntToken()
        {
            if (!Enumerator.HasToken()) throw new MicroassemblerParseException(Enumerator.Last, "Integer expected");
            if (Enumerator.Current.TokenType != TokenType.Integer) throw new MicroassemblerParseException(Enumerator.Current, "Integer expected");
            int retVal = (int)Enumerator.Current.Value;
            Enumerator.Advance();
            return retVal;
        }

        public String GetStringToken()
        {
            if (!Enumerator.HasToken()) throw new MicroassemblerParseException(Enumerator.Last, "String expected");
            if (Enumerator.Current.TokenType != TokenType.String) throw new MicroassemblerParseException(Enumerator.Current, "String expected");
            String retVal = (String)Enumerator.Current.Value;
            Enumerator.Advance();
            return retVal;
        }

        public String GetWordToken()
        {
            if (!Enumerator.HasToken()) throw new MicroassemblerParseException(Enumerator.Last, "Word expected");
            if (Enumerator.Current.TokenType != TokenType.Word) throw new MicroassemblerParseException(Enumerator.Current, "Word expected");
            String retVal = (String)Enumerator.Current.Value;
            Enumerator.Advance();
            return retVal;
        }

        public void DiscardOptionalToken(TokenType type, object token)
        {
            if (Enumerator.HasToken() && Enumerator.Current.TokenType == type && Enumerator.Current.Value.Equals(token)) Enumerator.Advance();
        }

        public void VerifySyntaxToken(TokenType type, object token)
        {
            if (!Enumerator.HasToken() || Enumerator.Current.TokenType != type || !Enumerator.Current.Value.Equals(token))
            {
                throw new MicroassemblerParseException($"'{token}' expected");
            }
            Enumerator.Advance();
        }

    }



    public class MicroassemblerParseException : Exception //Thrown if the parse stage encounters an error
    {
        public MicroassemblerParseException(Token token, String message) : base($"Error parsing microprogram: invalid token at or near '{token?.Value}' on line {token?.Line}") { }
        public MicroassemblerParseException(Token token) : base($"Error parsing microprogram: invalid token at or near '{token?.Value}' on line {token?.Line}") { }
        public MicroassemblerParseException(String message) : base(message) { }
    }



}
