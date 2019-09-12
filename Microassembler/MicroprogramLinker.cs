using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    class MicroprogramLinker
    {

        public List<Sequence> PlaceMicroprogram(Microprogram microprogram) //Assign each sequence an absolute starting address and return a list of all sequences in order.  Assign the fetch sequence to address 0
        {
            int currAddress = 0;
            List<Sequence> placedSequences = new List<Sequence>();
            Sequence fetchSequence = microprogram[microprogram.FetchEntrypoint] as Sequence;
            if (fetchSequence == null) throw new MicroassemblerLinkException($"Fetch sequence '{microprogram.FetchEntrypoint}' was not found");
            if (fetchSequence.IsMacro) throw new MicroassemblerLinkException($"Fetch sequence '{microprogram.FetchEntrypoint}' is defined as a Macro");
            fetchSequence.Address = 0;
            RecurseAssignBaseAddress(fetchSequence, 0);
            currAddress += fetchSequence.Steps.Count;
            placedSequences.Add(fetchSequence);
            foreach (KeyValuePair<String, Object> kv in microprogram.Symbols)
            {
                if (kv.Value is Sequence && kv.Value != fetchSequence)
                {
                    Sequence sequence = kv.Value as Sequence;
                    if (sequence.IsMacro) continue; //Do NOT place macros
                    sequence.Address = currAddress; //Assign sequence base address
                    RecurseAssignBaseAddress(sequence, currAddress);
                    currAddress += sequence.Steps.Count;
                    placedSequences.Add(sequence);
                }
            }
            if (currAddress > microprogram.MicroprogramLength - 1) throw new MicroassemblerLinkException($"Placed microprogram is of length {currAddress + 1} which exceeds the maximum length of {microprogram.MicroprogramLength}");
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
            //Resolve empty step
            SequenceAssertion emptyAssertion = microprogram.EmptyAssertion;
            if (microprogram.EmptyAssertion == null) throw new MicroassemblerLinkException("The empty/default sequence step is not defined");
            foreach (ControlWordLabel key in emptyAssertion.AssertedSignals.Keys.ToList())
            {
                Object value = emptyAssertion.AssertedSignals[key];
                if (!(value is int))
                {
                    String symbol = value.ToString();
                    Object resolvedSymbol = microprogram[symbol];
                    if (resolvedSymbol == null) throw new MicroassemblerLinkException($"Symbol {symbol} referenced by assertion on line {emptyAssertion.Line} is not defined");
                    if (resolvedSymbol is ISymbolResolver) resolvedSymbol = (resolvedSymbol as ISymbolResolver).Resolve();
                    emptyAssertion.AssertedSignals[key] = resolvedSymbol;
                }
            }
        }







    }

    public class MicroassemblerLinkException : Exception //Thrown if the link stage encounters an error
    {
        public MicroassemblerLinkException(String message) : base($"Error linking microprogram: {message}") { }
    }

}
