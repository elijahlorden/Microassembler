using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    public class MicroprogramExpander
    {
        public Microprogram Microprogram { get; private set; }

        public void ExpandMicroprogram(Microprogram microprogram) //Expands all macro references and resolves constants/macro variables
        {
            Microprogram = microprogram;
            Sequence sequence;

            do
            {
                sequence = microprogram.Symbols.Where(kv => ((kv.Value is Sequence) && (kv.Value as Sequence).Unexpanded && !(kv.Value as Sequence).IsMacro && (kv.Value as Sequence).Steps.Count > 0)).Select(kv => (Sequence)kv.Value).FirstOrDefault(); //Get first unexpanded sequence
                if (sequence == null) break;
                int macroAddr;
                for (macroAddr = 0; macroAddr < sequence.Steps.Count; macroAddr++) if (sequence.Steps[macroAddr].Type == SequenceStepType.MacroReference) break; //Find the first macro reference within the sequence
                SequenceMacroReference mRef = sequence.Steps[macroAddr] as SequenceMacroReference;
                if (microprogram[mRef.Symbol] == null || !(microprogram[mRef.Symbol] is Sequence)) throw new MicroassemblerExpansionException($"Macro {mRef.Symbol} referenced on line {mRef.Line} does not exist");
                Sequence macro = (Sequence)Microprogram[mRef.Symbol];
                if (macro.Parameters.Count > mRef.Arguments.Count) throw new MicroassemblerExpansionException($"Macro reference on line {mRef.Line} provides {mRef.Arguments.Count} arguments, while the referenced macro has {macro.Parameters.Count} parameters");
                List<SequenceStep> copiedSteps = macro.Steps.Select(s => (SequenceStep)s.Clone()).ToList();
                //Find a unique expansion symbol
                String expansionSymbol;
                int symNum = 0;
                do
                {
                    expansionSymbol = ((String.IsNullOrEmpty(mRef.ParentReference)) ? "_" : mRef.ParentReference + "._") + mRef.Symbol + "_" + symNum.ToString();
                    symNum++;
                }
                while (sequence[expansionSymbol] != null);
                //Recursively Offset all address symbols after the macro
                RecurseOffset(sequence, macroAddr, macro.Steps.Count - 1); //The macro call step is removed, so that is not counted as part of the offset
                //Create a container for symbols within the expanded macro and copy symbols from the original macro
                SymbolContainer expansionContainer = new SymbolContainer();
                macro.Symbols.ForEach(kv => expansionContainer.Symbols.Add(kv.Key, (kv.Value is ICloneable) ? (kv.Value as ICloneable).Clone() : kv.Value));
                sequence[expansionSymbol] = expansionContainer;
                expansionContainer.Symbols.ForEach(kv => OffsetObject(kv.Value, macroAddr));
                //Replace parameter references in copied steps with arguments, also add the expanded symbol name to any internal macro symbol references
                foreach (SequenceStep step in copiedSteps)
                {
                    if (step is SequenceMacroReference)
                    {
                        SequenceMacroReference stepRef = step as SequenceMacroReference;
                        if (stepRef.Symbol.Equals(mRef.Symbol)) throw new MicroassemblerExpansionException($"Caught recursive macro self-reference on line {stepRef.Line}"); //Sanity check
                        stepRef.Arguments = stepRef.Arguments.Select(a => SubArgumentsAndSymbols(a, macro, mRef, expansionSymbol)).ToList();
                        stepRef.ParentReference = expansionSymbol;
                    }
                    else if (step is SequenceAssertion)
                    {
                        SequenceAssertion stepAssertion = step as SequenceAssertion;
                        stepAssertion.AssertedSignals = stepAssertion.AssertedSignals.Select(kv => new KeyValuePair<ControlWordLabel, Object>(kv.Key, SubArgumentsAndSymbols(kv.Value, macro, mRef, expansionSymbol))).ToDictionary(kv => kv.Key, kv => kv.Value);
                    }
                }
                //Remove macro reference and insert steps
                sequence.Steps.RemoveAt(macroAddr);
                sequence.Steps.InsertRange(macroAddr, copiedSteps);
            }
            while (sequence != null);
        }

        public void OffsetObject(Object o, int offset)
        {
            if (o is SequenceLabel)
            {
                SequenceLabel ol = o as SequenceLabel;
                ol.LocalAddress += offset;
            }
        }

        public void RecurseOffset(SymbolContainer container, int startAddr, int offset)
        {
            foreach(KeyValuePair<String, Object> kv in container.Symbols)
            {
                Object o = kv.Value;
                if (o is SequenceLabel)
                {
                    SequenceLabel ol = o as SequenceLabel;
                    if (ol.LocalAddress > startAddr) ol.LocalAddress += offset;
                }
                if (o is SymbolContainer) RecurseOffset(o as SymbolContainer, startAddr, offset);
            }
        }

        public Object SubArgumentsAndSymbols(Object original, Sequence macro, SequenceMacroReference macroReference, String expansionSymbol) //Substitute paramater references with arguments, prepend expansion symbol to internal symbol references
        {
            if (!(original is String)) return original; //No operation required if original is a literal value
            String value = original as String;
            return (macro.Parameters.Contains(value)) ? macroReference.Arguments[macro.Parameters.IndexOf(value)]
                   : (macro.Symbols.ContainsKey(value)) ? expansionSymbol + "." + value
                   : value;
        }


    }

    public class MicroassemblerExpansionException : Exception //Thrown if the expansion stage encounters an error
    {
        public MicroassemblerExpansionException(String message) : base($"Error expanding microprogram: {message}") { }
    }


}
