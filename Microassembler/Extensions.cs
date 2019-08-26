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


    }
}
