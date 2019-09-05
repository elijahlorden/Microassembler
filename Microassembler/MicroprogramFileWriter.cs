using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{

    class MicroprogramFileWriter
    {

        public static void WriteMicroprogram(Microprogram microprogram, List<Sequence> placedSequences, String path)
        {




        }

        public static String GetMicroprogramString(Microprogram microprogram, List<Sequence> placedSequences)
        {
            int filled = 0;
            String program = "";
            foreach (Sequence sequence in placedSequences)
            {
                program += $"//Sequence '{sequence.Symbol}' at address {sequence.Address}\n";
                BitArray[] steps = ProcessSequence(sequence, microprogram);
                filled += steps.Length;
                steps.EachIndex((s, i) =>
                {
                    program += s.ToBitString();
                    String labels = GetLocalLabels(sequence, i);
                    if (!String.IsNullOrEmpty(labels)) program += $"   //{labels} (address {sequence.Address + i})";
                    program += "\n";
                });
                program += "\n";
            }
            //Fill the rest of the program with the defined empty step
            program += $"//Empty space ({microprogram.MicroprogramLength - filled} unused addresses)\n";
            String emptyStep = ProcessStep(microprogram.EmptyAssertion, microprogram).ToBitString();
            for (int i = 0; i < microprogram.MicroprogramLength - filled; i++) program += emptyStep + "\n";
            return program;
        }

        public static String GetLocalLabels(SymbolContainer container, int labelAddress, String parentSymbol = "")
        {
            String labels = "";
            foreach(KeyValuePair<String, Object> kv in container.Symbols)
            {
                if (kv.Value is SymbolContainer)
                {
                    labels += GetLocalLabels(kv.Value as SymbolContainer, labelAddress, (parentSymbol.Length > 0) ? parentSymbol + "." + kv.Key : kv.Key);
                }
                else if (kv.Value is SequenceLabel && (kv.Value as SequenceLabel).LocalAddress == labelAddress)
                {
                    labels += $" ::{((parentSymbol.Length > 0) ? parentSymbol + "." : "") + kv.Key}:: |";
                }
            }
            return labels;
        }

        public static BitArray[] ProcessSequence(Sequence sequence, Microprogram microprogram)
        {
            BitArray[] arrays = new BitArray[sequence.Steps.Count];
            sequence.Steps.EachIndex((s,i) => arrays[i] = ProcessStep(s, microprogram));
            return arrays;
        }

        public static BitArray ProcessStep(SequenceStep step, Microprogram microprogram)
        {
            if (step.Type == SequenceStepType.MacroReference) throw new MicroassemblerWriteException($"Unexpanded macro found on line {step.Line}");
            SequenceAssertion assertion = step as SequenceAssertion;
            BitArray array = new BitArray(microprogram.ControlWordWidth);
            array[microprogram.BankSelectorMask] = (ulong)assertion.Bank;
            int bankOffset = microprogram.BankSelectorMask.Length;
            foreach(KeyValuePair<ControlWordLabel, Object> signal in assertion.AssertedSignals)
            {
                if (!(signal.Value is int)) throw new MicroassemblerWriteException($"Invalid/unresolved symbol {signal.Value} in assertion on line {assertion.Line}");
                int value = (int)signal.Value;
                if (value > (int)signal.Key.Mask.MaxValue) Console.WriteLine($"Warning: Asserted signal {signal.Key.Name} on line {assertion.Line} has a maximum value of {signal.Key.Mask.MaxValue} (actual assertion of {value} will be truncated)");
                array[signal.Key.Mask, bankOffset] = (ulong)value;
            }
            return array;
        }
    }

    public class MicroassemblerWriteException : Exception //Thrown if the write stage encounters an error
    {
        public MicroassemblerWriteException(String message) : base($"Error writing microprogram: {message}") { }
    }

}
