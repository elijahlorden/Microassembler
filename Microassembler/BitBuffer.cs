﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    public class BitArray
    {
        private Boolean[] Buffer;

        public int Length
        {
            get
            {
                return Buffer.Length;
            }
        }

        public int this[int i] //Single-bit indexer
        {
            get
            {
                if (i >= Buffer.Length || i < 0) throw new IndexOutOfRangeException(i.ToString());
                return (Buffer[i]) ? 1 : 0;
            }
            set
            {
                if (i >= Buffer.Length || i < 0) throw new IndexOutOfRangeException(i.ToString());
                Buffer[i] = (value == 0) ? false : true;
            }
        }

        public ulong this[int StartIndex, int EndIndex] //Multi-bit indexer
        {
            get
            {
                if (StartIndex >= Buffer.Length || StartIndex < 0) throw new IndexOutOfRangeException("StartIndex " + StartIndex);
                if (EndIndex >= Buffer.Length || EndIndex < 0) throw new IndexOutOfRangeException("EndIndex " + EndIndex);
                int sign = (StartIndex > EndIndex) ? 1 : -1;
                ulong retVal = 0;
                int place = 0;
                for (int i=EndIndex; i != StartIndex; i += sign)
                {
                    if (Buffer[i]) retVal |= 1ul << place;
                    place++;
                }
                if (Buffer[StartIndex]) retVal |= 1ul << (place);
                return retVal;
            }
            set
            {
                if (StartIndex >= Buffer.Length || StartIndex < 0) throw new IndexOutOfRangeException("StartIndex " + StartIndex);
                if (EndIndex >= Buffer.Length || EndIndex < 0) throw new IndexOutOfRangeException("EndIndex " + EndIndex);
                int sign = (StartIndex > EndIndex) ? 1 : -1;
                ulong val = value;
                int place = 0;
                for (int i = EndIndex; i != StartIndex; i += sign)
                {
                    Buffer[i] = ((val & (1ul << place)) > 0);
                    place++;
                }
                Buffer[StartIndex] = ((val & (1ul << place)) > 0);
            }
        }

        public ulong this[BitMask mask]
        {
            get => this[mask.Msb, mask.Lsb];
            set => this[mask.Msb, mask.Lsb] = value;
        }

        public ulong this[BitMask mask, int offset]
        {
            get => this[mask.Msb + offset, mask.Lsb + offset];
            set => this[mask.Msb + offset, mask.Lsb + offset] = value;
        }

        public BitArray(int Length)
        {
            Buffer = new bool[Length];
        }

        public BitArray(BitArray other)
        {
            Buffer = new bool[other.Length];
            other.Buffer.CopyTo(Buffer, 0);
        }

        public String ToBitString()
        {
            String s = "";
            for (int i=0; i < Length; i++)
            {
                s = ((Buffer[i]) ? "1" : "0") + s;
            }
            return s;
        }

        public override string ToString() => ToBitString();

    }

    public class BitMask
    {
        public int Msb { get; set; }
        public int Lsb { get; set; }

        public int LowerBound { get => Math.Min(Msb, Lsb); }
        public int UpperBound { get => Math.Max(Msb, Lsb); }

        public int Length
        {
            get
            {
                return Math.Abs(Msb - Lsb) + 1;
            }
        }
        
        public ulong MaxValue
        {
            get
            {
                return (ulong)Math.Pow(2, Length) - 1;
            }
        }

        public BitMask(int Msb, int Lsb)
        {
            this.Msb = Msb;
            this.Lsb = Lsb;
        }

        public BitMask(int bit) : this(bit, bit) { }

        public Boolean OverlapsWith(BitMask other)
        {
            if ((UpperBound > other.UpperBound && LowerBound > other.UpperBound) || (UpperBound < other.LowerBound && LowerBound < other.LowerBound)) return false;
            return true;
        }

        public long ToLongMask()
        {
            long mask = 0;
            if (UpperBound > 63) throw new IndexOutOfRangeException("Mask cannot be converted to a long, too large");
            if (UpperBound == LowerBound) return (long)Math.Pow(2, UpperBound);
            for (int i = LowerBound; i <= UpperBound; i++) mask |= (long)Math.Pow(2, i);
            return mask;
        }

        public override string ToString()
        {
            return (Msb == Lsb) ? Msb.ToString() :  $"{Msb}:{Lsb}";
        }

    }


}
