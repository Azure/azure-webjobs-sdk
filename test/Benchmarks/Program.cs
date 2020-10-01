using System;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
    class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
