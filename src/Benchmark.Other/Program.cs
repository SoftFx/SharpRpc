using System;

namespace Benchmark.Other
{
    class Program
    {
        static void Main(string[] args)
        {
            new SerializersBenchmark().Run();

            Console.Read();
        }
    }
}
