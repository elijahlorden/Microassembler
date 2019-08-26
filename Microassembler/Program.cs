using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microassembler
{
    class Program
    {
        static void Main(string[] args)
        {
            StreamReader reader = new StreamReader("Microprogram.mal");
            String file = reader.ReadToEnd();
            reader.Dispose();
            MicroprogramParser parser = new MicroprogramParser();
            Microprogram microprogram = parser.ParseProgram(file);
            Console.WriteLine();


        }
    }
}
