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
                        case "macro":
                            if (!ProcessMacro()) return null;
                            break;
                        case "empty":
                            if (!ParseEmpty()) return null;
                            break;
                        case "entrypoints":
                            if (!ParseEntrypoints()) return null;
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
            //Config sanity checks
            if (Microprogram.MicroprogramLength <= 0) throw new MicroassemblerParseException($"The microprogram length must be greater than zero");
            if (Microprogram.OpcodeWidth <= 0) throw new MicroassemblerParseException($"The opcode width must be greater than zero");
            if (Microprogram.ControlWordWidth <= 0) throw new MicroassemblerParseException($"The control word width must be greater than zero");
            if (Microprogram.BankSelectorMask.Length <= 0) throw new MicroassemblerParseException($"The bank selector width must be greater than zero");
            if (Microprogram.BankSelectorMask.Length + 1 >= Microprogram.ControlWordWidth) throw new MicroassemblerParseException($"Control word width of {Microprogram.ControlWordWidth} is invalid, as the bank selector has a width of {Microprogram.BankSelectorMask.Length}");
            //Check control word label lengths
            foreach(ControlWordLabel cw in Microprogram.ControlWordLabels.Values)
            {
                if (cw.Mask.Length <= 0) throw new MicroassemblerParseException($"Control word label {cw.Name} must have a length that is geater than zero");
                if (cw.Mask.Length + Microprogram.BankSelectorMask.Length > Microprogram.ControlWordWidth) throw new MicroassemblerParseException($"Control word label {cw.Name} exceeds the maximum control word width of {Microprogram.ControlWordWidth - Microprogram.BankSelectorMask.Length}");
            }
            return Microprogram;
        }

        public Boolean ParseEntrypoints()
        {
            VerifySyntaxToken(TokenType.OpenBlock, "{");
            while (true)
            {
                if (!Enumerator.HasToken()) throw new MicroassemblerParseException(Enumerator.Last, "Pair or } expected");
                if (Enumerator.Current.TokenType == TokenType.CloseBlock)
                {
                    Enumerator.Advance();
                    break;
                }
                if (Enumerator.Current.TokenType == TokenType.Pair)
                {
                    Object[] pair = Enumerator.Current.Value as Object[];
                    Enumerator.Advance();
                    if (!(pair[1] is String)) throw new MicroassemblerParseException(Enumerator.Last, "Entrypoint pair value must be a symbol");
                    String symbol = (String)pair[1];
                    if (pair[0] is String)
                    {
                        String key = pair[0] as String;
                        switch (key.ToLower())
                        {
                            case "fetch":
                                Microprogram.FetchEntrypoint = symbol;
                                break;
                            case "interrupt":
                                Microprogram.InterruptEntrypoint = symbol;
                                break;
                            default: throw new MicroassemblerParseException(Enumerator.Last, $"Invalid entrypoint '{key}'");
                        }

                    }
                    else if (pair[0] is int)
                    {
                        int entryIndex = (int)pair[0];
                        if (Microprogram.InstructionEntrypoints.ContainsKey(entryIndex)) Console.WriteLine($"Warning: Duplicate entrypoint definition on line {Enumerator.Last.Line}, previous value overridden");
                        Microprogram.InstructionEntrypoints[entryIndex] = symbol;
                    }
                    else throw new MicroassemblerParseException(Enumerator.Last, "Invalid Pair key");

                    if (Enumerator.HasNext() && Enumerator.Next.TokenType == TokenType.Pair) VerifySyntaxToken(TokenType.ListDelimeter, ",");
                    if (Enumerator.HasNext() && Enumerator.Next.TokenType == TokenType.CloseBlock) DiscardOptionalToken(TokenType.ListDelimeter, ",");
                }
                else throw new MicroassemblerParseException(Enumerator.Current, "Pair expected");
            }
            return true;
        }

        public Boolean ParseEmpty()
        {
            SequenceAssertion assertion = ParseSequenceAssertion();
            Microprogram.EmptyAssertion = assertion;
            return true;
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

        public Boolean ProcessMacro() //Process a macro
        {
            String symbol = GetWordToken();
            Sequence sequence = ParseSequence(true);
            CheckSymbolWarning(symbol, Enumerator.Last.Line);
            Microprogram[symbol] = sequence;
            Console.WriteLine($"Processed macro '{symbol}'");
            return true;
        }

        public Sequence ParseSequence(Boolean isMacro) // Parses a sequence and return an object representation (used for both normal sequences and macros)
        {
            Sequence sequence = new Sequence() { IsMacro = isMacro };
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
                if (Enumerator.HasToken() && Enumerator.Current.TokenType == TokenType.CloseBlock)
                {
                    Enumerator.Advance();
                    break;
                }
                word = GetWordToken();
                if (word.StartsWith("::") && word.EndsWith("::"))
                {
                    String label = word.Substring(2, word.Length - 4);
                    sequence[label] = new SequenceLabel { LocalAddress = sequence.Steps.Count };
                }
                else if (Enumerator.HasToken() && Enumerator.Current.TokenType == TokenType.ParenList)
                {
                    List<Object> arguments = (List<Object>)Enumerator.Current.Value;
                    Enumerator.Advance();
                    SequenceMacroReference step = new SequenceMacroReference() { Arguments = arguments, Symbol = word, Line = Enumerator.Last.Line };
                    sequence.Steps.Add(step);
                }
                else if (word.ToLower().Equals("assert"))
                {
                    SequenceAssertion step = ParseSequenceAssertion();
                    sequence.Steps.Add(step);
                }
                else
                {
                    throw new MicroassemblerParseException(Enumerator.Last);
                }
            }
            while (true);

            return sequence;
        }

        public SequenceAssertion ParseSequenceAssertion() //Parses an assertion statement
        {
            SequenceAssertion assertion = new SequenceAssertion();
            VerifySyntaxToken(TokenType.OpenBlock, "{");
            int startLine = Enumerator.Last.Line;
            assertion.Line = startLine;
            while (true)
            {
                if (!Enumerator.HasToken()) throw new MicroassemblerParseException($"Unfinished assertion statement on line {startLine}");
                Token token = Enumerator.Current;
                Enumerator.Advance();
                if (token.TokenType == TokenType.CloseBlock)
                {
                    break;
                }
                else if (token.TokenType == TokenType.Pair)
                {
                    Object labelName = (token.Value as Object[])[0];
                    Object labelValue = (token.Value as Object[])[1];
                    if (!(labelName is String)) throw new MicroassemblerParseException($"Invalid assertion key on line {token.Line}");
                    if (!Microprogram.ControlWordLabels.ContainsKey(labelName.ToString())) throw new MicroassemblerParseException($"Nonexistant control word label on line {token.Line}");
                    assertion.AddAssertion(Microprogram.ControlWordLabels[labelName.ToString()], labelValue);
                    if (Enumerator.HasNext() && Enumerator.Next.TokenType == TokenType.Pair) VerifySyntaxToken(TokenType.ListDelimeter, ",");
                    if (Enumerator.HasNext() && Enumerator.Next.TokenType == TokenType.CloseBlock) DiscardOptionalToken(TokenType.ListDelimeter, ",");
                }
                else
                {
                    throw new MicroassemblerParseException(token);
                }
            }

            if (assertion.AssertedSignals.Count > 0)
            {
                List<ControlWordLabel> overlaps = assertion.AssertedSignals.Where(signal1 => assertion.AssertedSignals.Any(signal2 => (!signal2.Equals(signal1) && signal1.Key.Mask.OverlapsWith(signal2.Key.Mask)))).Select(kv => kv.Key).ToList();
                if (overlaps.Count > 0)
                {
                    Console.WriteLine($"Warning: Assertion on line {startLine} contains overlapping signals: {String.Join(", ", overlaps.Select(o => o.Name))}");
                }
            }
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
            if (!Enumerator.HasToken()) throw new MicroassemblerParseException(Enumerator.Current, "Number or Number:Number pair expected");
            int msb, lsb;
            if (Enumerator.Current.TokenType == TokenType.Pair)
            {
                Object[] arr = (Object[])Enumerator.Current.Value;
                if (!(arr[0] is int) || !(arr[1] is int)) throw new MicroassemblerParseException("Number:Number pair expected");
                msb = (int)arr[0];
                lsb = (int)arr[1];
                Enumerator.Advance();
            }
            else
            {
                msb = GetIntToken();
                lsb = msb;
            }
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
                        Microprogram.BankSelectorMask = new BitMask(arg, 0);
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
        public MicroassemblerParseException(Token token, String message) : base($"Error parsing microprogram: invalid token at or near '{((token.Value.GetType().IsArray) ? (token.Value as Object[]).ToListString() : token.Value.ToString())}' on line {token?.Line}") { }
        public MicroassemblerParseException(Token token) : base($"Error parsing microprogram: invalid token at or near '{((token.Value.GetType().IsArray) ? (token.Value as Object[]).ToListString() : token.Value.ToString())}' on line {token?.Line}") { }
        public MicroassemblerParseException(String message) : base(message) { }
    }



}
