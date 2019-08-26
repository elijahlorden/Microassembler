using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{

    public class Microprogram : SymbolContainer
    {
        public int ControlWordWidth { get; set; }
        public int OpcodeWidth { get; set; }
        public int MicroprogramLength { get; set; }
        public BitMask BankSelectorMask { get; set; }

        public String ProgramRomName { get; set; }
        public String EntrypointRomName { get; set; }

        public Dictionary<String, ControlWordLabel> ControlWordLabels { get; private set; }

        public Microprogram()
        {
            ControlWordLabels = new Dictionary<string, ControlWordLabel>();
        }

    }

    public class ControlWordLabel
    {
        public String Name { get; set; }
        public BitMask Mask { get; set; }
        public int Bank { get; set; }
    }

    public class SequenceLabel
    {
        public int PreExpansionAddress { get; set; }
        public int PostExpansionAddress { get; set; }
        public int AbsoluteAddress { get; set; }
    }

    public class Sequence : SymbolContainer
    {
        public List<SequenceStep> Steps { get; private set; }
        public List<String> Parameters { get; private set; }
        public ulong Address { get; set; }

        public Sequence()
        {
            Steps = new List<SequenceStep>();
            Parameters = new List<string>();
        }

    }

    public enum SequenceStepType
    {
        Assertion,
        MacroReference
    }

    public class SequenceStep
    {
        public SequenceStepType Type { get; protected set; }
        public virtual SequenceStep Duplicate() => new SequenceStep { Type = Type };
    }

    public class SequenceAssertion : SequenceStep
    {
        public BitArray ControlWord { get; private set; }
        public Dictionary<ControlWordLabel, Object> AssertedSignals { get; private set; }
        public int Bank { get; private set; }

        public SequenceAssertion(int ControlWordLength)
        {
            Type = SequenceStepType.Assertion;
            ControlWord = new BitArray(ControlWordLength);
            AssertedSignals = new Dictionary<ControlWordLabel, Object>();
        }

        private SequenceAssertion() { }

        public override SequenceStep Duplicate() => new SequenceAssertion() { Type = Type, ControlWord = new BitArray(ControlWord), Bank = Bank, AssertedSignals = AssertedSignals.ToDictionary(e => e.Key, e => e.Value) };
    }

    public class SequenceMacroReference : SequenceStep
    {
        public String Symbol { get; set; }
        public List<Object> Arguments { get; set; }

        public SequenceMacroReference()
        {
            Type = SequenceStepType.MacroReference;
            Arguments = new List<Object>();
        }

        public override SequenceStep Duplicate() => new SequenceMacroReference() { Type = Type, Symbol = Symbol, Arguments = Arguments.ToList() };
    }

}
