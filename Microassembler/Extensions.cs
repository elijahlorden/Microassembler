using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    public static class Extensions
    {

        public static String ToListString(this Array array) => string.Join(",", (Object[])array);

        public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T item in enumeration)
            {
                action(item);
            }
        }

        public static void EachIndex<T>(this IEnumerable<T> ie, Action<T, int> a)
        {
            int i = 0;
            foreach (T e in ie) a(e, i++);
        }

    }
}
