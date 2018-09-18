using System;

namespace NetCoreRunner
{
    /// <summary>
    /// Simple invoke executable for running with .Net core
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Ceen.Httpd.Cli.Program.Main(args);
        }
    }
}
