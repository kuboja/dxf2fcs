using CommandLine;
using System;
using System.IO;

namespace dxf2fcs
{
    public class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<ProgramOptions>(args)
                .WithParsed(opts => MainOk(opts));
        }

        static void MainOk(ProgramOptions options) { 
            var dxfFilePath = options.DxfPath;

            if (!new FileInfo(dxfFilePath).Exists)
            {
                Console.WriteLine("Error: The dxf file does not exist!");
                return;
            }

            var fcsFilePath = !string.IsNullOrWhiteSpace(options.FcsFile)
                ? options.FcsFile
                : dxfFilePath.Replace(".dxf", ".fcs", true, System.Globalization.CultureInfo.CurrentCulture);

            try
            {
                var loader = new DxfLoader(options.Unit, options.Precision);
                var fcs = loader.ToFcs(dxfFilePath);

                using (var sw = new StreamWriter(fcsFilePath))
                {
                    sw.Write(fcs);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while parse dxf and creating fcs:");
                Console.WriteLine(ex.Message);
                return;
            }

            Console.WriteLine("Done.");
        }
    }
}
