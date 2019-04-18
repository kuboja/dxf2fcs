using CommandLine;
using System;
using System.IO;

namespace dxf2fcs
{
    public class Program
    {
        static void Main(string[] args)
        {
            var parser = new Parser(with =>
            {
                with.CaseInsensitiveEnumValues = false;
                with.CaseSensitive = false;
            });

            var arguments = parser.ParseArguments<ProgramOptions>(args);

            if (!(arguments is Parsed<ProgramOptions> options))
            {
                return;
            }

            var dxfFilePath = options.Value.DxfPath;

            if (!new FileInfo(dxfFilePath).Exists)
            {
                Console.WriteLine("Error: The dxf file does not exist!");
                return;
            }

            var fcsFilePath = !string.IsNullOrWhiteSpace(options.Value.FcsFile)
                ? options.Value.FcsFile
                : dxfFilePath.Replace(".dxf", ".fcs", true, System.Globalization.CultureInfo.CurrentCulture);

            try
            {
                var loader = new DxfLoader(options.Value.Unit, options.Value.Precision);
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

            //Console.WriteLine(sb);
            Console.WriteLine("Done.");
        }
    }
}
