using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    public class SymbolContainer
    {
        public Dictionary<String, Object> Symbols { get; private set; }
        public SymbolContainer Parent { get; private set; }

        public SymbolContainer Root
        {
            get => (Parent != null) ? Parent.Root : this;
        }

        public String Symbol
        {
            get => (Parent != null) ? Parent.Symbols.FirstOrDefault(p => p.Value == this).Key : "";
        }

        public String FullSymbol
        {
            get => (Parent != null) ? ((Parent.Parent != null) ? Parent.FullSymbol + "." : "") + Parent.Symbols.FirstOrDefault(p => p.Value == this).Key : "";
        }

        public SymbolContainer()
        {
            Symbols = new Dictionary<string, object>();
        }

        public Object this[String key]
        {
            get
            {
                if (key.Contains('.'))
                {
                    int index = key.IndexOf('.');
                    String localkey = key.Substring(0, index);
                    if (Symbols.ContainsKey(localkey))
                    {
                        if (Symbols[localkey] is SymbolContainer)
                        {
                            String childkey = key.Substring(index + 1);
                            return ((SymbolContainer)Symbols[localkey])[childkey];
                        }
                    }
                }
                return (Symbols.ContainsKey(key)) ? Symbols[key] : null;
            }
            set
            {
                if (key.Contains('.'))
                {
                    int index = key.IndexOf('.');
                    String localkey = key.Substring(0, index);
                    if (Symbols.ContainsKey(localkey))
                    {
                        if (Symbols[localkey] is SymbolContainer)
                        {
                            String childkey = key.Substring(index + 1);
                            ((SymbolContainer)Symbols[localkey])[childkey] = value;
                        }
                    }
                }
                else
                {
                    Symbols[key] = value;
                    if (value is SymbolContainer) (value as SymbolContainer).Parent = this;
                }
            }
        }

    }
}
