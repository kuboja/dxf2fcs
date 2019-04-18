using System;
using System.IO;

namespace dxf2fcs
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.WriteLine("Missing argument: path to dxf file");
                return;
            }

            var dxfFilePath = args[0];
            var fcsFilePath = dxfFilePath.Replace(".dxf", ".fcs", true, System.Globalization.CultureInfo.CurrentCulture);

            var loader = new DxfLoader();
            var fcs = loader.ToFcs(dxfFilePath);

            using (var sw = new StreamWriter(fcsFilePath))
            {
                sw.Write(fcs);
            }

            //Console.WriteLine(sb);
            Console.WriteLine("Done.");
        }
    }
}
