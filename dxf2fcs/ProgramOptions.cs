using CommandLine;

namespace dxf2fcs
{
    public class ProgramOptions
    {
        [Value(0, Required = true, MetaName = "dxf path", HelpText = "Full path to dxf file.")]
        // [Option('i', "read", Required = true, HelpText = "Input files to be processed.")]
        public string? DxfPath { get; set; }

        // Omitting long name, defaults to name of property, ie "--verbose"
        [Option('o', "output",
          HelpText = "Full path to output fcs file.")]
        public string? FcsFile { get; set; }

        [Option('p', "precision",
          Default = 5,
          HelpText = "Number of decimal places of coordinates.")]
        public int Precision { get; set; }

        [Option('u', "unit", HelpText = "Unit for conversion.", Default = Units.mm)]
        public Units Unit { get; set; }
    }
}
