using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipTest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 3) { PrintUsage(); return; }

            Console.WriteLine("Processing...");
            var timer = new Stopwatch();

            var pgzip = new ParallelGZipArchiver();

            try
            {
                switch (args[0])
                {
                    case "compress":
                        timer.Start();

                        using (FileStream source = new FileStream(args[1], FileMode.Open))
                        using (FileStream destination = new FileStream(args[2], FileMode.Create))
                        {
                            if (!pgzip.Compress(source, destination))
                            {
                                PrintError(pgzip.Exception);
                                Environment.Exit(1);
                            }
                        }

                        timer.Stop();
                        break;

                    case "decompress":
                        timer.Start();

                        using (FileStream source = new FileStream(args[1], FileMode.Open))
                        using (FileStream destination = new FileStream(args[2], FileMode.CreateNew))
                        {
                            if (!pgzip.Decompress(source, destination))
                            {
                                PrintError(pgzip.Exception);
                                Environment.Exit(1);
                            }
                        }

                        timer.Stop();
                        break;

                    default:
                        PrintUsage();
                        return;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error! " + e.Message);
                Environment.Exit(2);
            }
            Console.WriteLine($"Success! Elapsed time: {timer.ElapsedMilliseconds}ms");
        }
        
        public static void PrintError(Exception e)
        {
            Console.WriteLine("Error!");
            Console.WriteLine(e.GetType().ToString());
            Console.WriteLine($"{e.Message}\nIn {e.Source}.{e.TargetSite}");
            Console.WriteLine(e.StackTrace);
        }

        public static void PrintUsage()
        {
            Console.WriteLine("Usage: compress|decompress source destination");
        }
    }
}