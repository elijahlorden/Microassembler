using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    class MicroprogramLinker
    {

        public List<Sequence> PlaceMicroprogram(Microprogram microprogram) //Assign each sequence an absolute starting address and return a list of all sequences in order
        {
            int currAddress = 0;
            List<Sequence> placedSequences = new List<Sequence>();
            foreach (KeyValuePair<String, Object> kv in microprogram.Symbols)
            {
                if (kv.Value is Sequence)
                {
                    Sequence sequence = kv.Value as Sequence;
                    if (sequence.IsMacro) continue; //Do NOT place macros
                    sequence.Address = currAddress; //Assign sequence base address
                    RecurseAssignBaseAddress(sequence, currAddress);
                    currAddress += sequence.Steps.Count;
                    placedSequences.Add(sequence);
                }
            }
            return placedSequences;
        }

        public void RecurseAssignBaseAddress(SymbolContainer container, int address) //Recursively assign a sequence base address to all symbols
        {
            foreach (KeyValuePair<String, Object> kv in container.Symbols)
            {
                Object o = kv.Value;
                if (o is SequenceLabel) (o as SequenceLabel).BaseAddress = address;
                if (o is SymbolContainer) RecurseAssignBaseAddress(o as SymbolContainer, address);
            }
        }

        public void ResolveSymbols(Microprogram microprogram, List<Sequence> placedSequences) //Resolve all symbol references to numeric values
        {
            foreach(Sequence sequence in placedSequences)
            {
                foreach(SequenceStep step in sequence.Steps)
                {
                    if (step is SequenceMacroReference) throw new MicroassemblerLinkException($"Encountered unexpanded macro on line {step.Line}");
                    if (step is SequenceAssertion)
                    {
                        SequenceAssertion assertion = step as SequenceAssertion;
                        foreach(ControlWordLabel key in assertion.AssertedSignals.Keys.ToList())
                        {
                            Object value = assertion.AssertedSignals[key];
                            if (!(value is int))
                            {
                                String symbol = value.ToString();
                                Object resolvedSymbol = (sequence[symbol] != null) ? sequence[symbol] : microprogram[symbol];
                                if (resolvedSymbol == null) throw new MicroassemblerLinkException($"Symbol {symbol} referenced by assertion on line {assertion.Line} is not defined");
                                if (resolvedSymbol is ISymbolResolver) resolvedSymbol = (resolvedSymbol as ISymbolResolver).Resolve();
                                assertion.AssertedSignals[key] = resolvedSymbol;
                            }
                        }
                    }
                }
            }
        }







    }

    public class MicroassemblerLinkException : Exception //Thrown if the expansion stage encounters an error
    {
        public MicroassemblerLinkException(String message) : base($"Error expanding microprogram: {message}") { }
    }

}
