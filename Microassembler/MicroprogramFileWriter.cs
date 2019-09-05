using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    enum ROMFormat { Hexadecimal, Binary }

    class MicroprogramFileWriter
    {

        public static void WriteMicroprogram(Microprogram microprogram, String path, ROMFormat format)
        {

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
                if (value > (int)signal.Key.Mask.MaxValue) Console.WriteLine($"Warning: Asserted signal {signal.Key.Name} on line {assertion.Line} has a maximum value of {signal.Key.Mask.MaxValue} (actual assertion will be truncated)");
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
