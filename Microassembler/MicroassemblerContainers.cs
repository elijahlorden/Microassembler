using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microassembler
{

    public class Microprogram : SymbolContainer
    {
        public long ControlWordWidth { get; set; }
        public long OpcodeWidth { get; set; }
        public long MicroprogramLength { get; set; }
        public BitMask BankSelectorMask { get; set; }

        public ulong BanksCount { get => BankSelectorMask.MaxValue; }

        public Dictionary<String, ControlWordLabel> ControlWordLabels { get; private set; }
        public Dictionary<int, String> InstructionEntrypoints { get; private set; }

        public String FetchEntrypoint { get; set; }
        public String InterruptEntrypoint { get; set; }

        public SequenceAssertion EmptyAssertion { get; set; }

        public Microprogram()
        {
            ControlWordLabels = new Dictionary<string, ControlWordLabel>();
            InstructionEntrypoints = new Dictionary<int, string>();
        }

    }

    public interface ISymbolResolver
    {
        int Resolve();
    }

    public class ControlWordLabel
    {
        public String Name { get; set; }
        public BitMask Mask { get; set; }
        public int Bank { get; set; }
    }

    public class SequenceLabel : ICloneable, ISymbolResolver
    {
        public int LocalAddress { get; set; }
        public int BaseAddress { get; set; }
        public int AbsoluteAddress { get => BaseAddress + LocalAddress; }

        public Object Clone() => new SequenceLabel { LocalAddress = LocalAddress, BaseAddress = BaseAddress };

        public int Resolve() => AbsoluteAddress;

    }

    public class Sequence : SymbolContainer, ISymbolResolver
    {
        public List<SequenceStep> Steps { get; private set; }
        public List<String> Parameters { get; private set; }
        public int Address { get; set; }
        public Boolean Unexpanded { get => Steps.Any(s => s.Type == SequenceStepType.MacroReference); }
        public Boolean IsMacro { get; set; }

        public Sequence()
        {
            Steps = new List<SequenceStep>();
            Parameters = new List<string>();
        }

        public int Resolve() => Address;

    }

    public enum SequenceStepType
    {
        Assertion,
        MacroReference
    }

    public class SequenceStep : ICloneable
    {
        public int Line { get; set; }
        public SequenceStepType Type { get; protected set; }
        public virtual Object Clone() => new SequenceStep { Type = Type, Line = Line };
    }

    public class SequenceAssertion : SequenceStep
    {
        public Dictionary<ControlWordLabel, Object> AssertedSignals { get; set; }
        public int Bank { get; private set; }

        public SequenceAssertion()
        {
            Type = SequenceStepType.Assertion;
            AssertedSignals = new Dictionary<ControlWordLabel, Object>();
        }

        public void AddAssertion(ControlWordLabel label, Object value, int line = -1)
        {
            if (AssertedSignals.Count > 0 && Bank != label.Bank) throw new MicroassemblerParseException(((line == -1) ? "Assertion attempts" : $"Assertion on line {line} attempts") +  $" to assert a signal '{label.Name}' on bank {label.Bank} while already on bank {Bank}");
            Bank = label.Bank;
            AssertedSignals.Add(label, value);
        }

        public override Object Clone() => new SequenceAssertion() { Bank = Bank, Line = Line, AssertedSignals = AssertedSignals.ToDictionary(e => e.Key, e => e.Value) };
    }

    public class SequenceMacroReference : SequenceStep
    {
        public String Symbol { get; set; }
        public List<Object> Arguments { get; set; }
        public String ParentReference { get; set; }

        public SequenceMacroReference()
        {
            Type = SequenceStepType.MacroReference;
            Arguments = new List<Object>();
        }

        public override Object Clone() => new SequenceMacroReference() { Symbol = Symbol, Line = Line, ParentReference = ParentReference, Arguments = Arguments.ToList() };
    }

    public class SymbolSelector
    {
        public BitMask Selector { get; set; }
        public String Symbol { get; set; }

        public static Regex parseRegx = new Regex(@"([^\[\]]+)\[(?:(\d+):(\d+)|(\d+))\]", RegexOptions.Compiled);

        public static SymbolSelector TryParse(String value)
        {
            Match match = parseRegx.Match(value);
            if (match.Success)
            {
                SymbolSelector selector = new SymbolSelector();
                selector.Symbol = match.Groups[1].Value;
                if (match.Groups[4].Success) //If selector is a single bit
                {
                    selector.Selector = new BitMask(int.Parse(match.Groups[4].Value));
                }
                else
                {
                    selector.Selector = new BitMask(int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
                }
                return selector;
            }
            return null;
        }


    }

}
