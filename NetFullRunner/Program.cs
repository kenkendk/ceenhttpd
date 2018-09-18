using System;

namespace NetFullRunner
{
    /// <summary>
    /// Simple invoke executable for running with .Net core
    /// </summary>
    class MainClass
    {
        public static void Main(string[] args)
        {
            Ceen.Httpd.Cli.Program.Main(args);
        }
    }
}
