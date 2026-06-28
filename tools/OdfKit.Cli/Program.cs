using OdfKit.Cli;
using OdfKit.Compliance;

try
{
    return OdfKitCli.Run(args, Console.Out, Console.Error);
}
catch (Exception ex)
{
    Console.Error.WriteLine(OdfLocalizer.GetMessage("Cli_UnhandledError", ex.Message));
    return 2;
}
