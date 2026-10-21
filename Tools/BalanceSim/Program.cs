using System;
using System.Diagnostics;
using HouseEdge.Core;

// dotnet run -c Release -- [runs] [first seed] [archetypes]
// "archetypes" also plays one batch per archetype focus
class Program
{
    static void Main(string[] args)
    {
        int runs = args.Length > 0 ? int.Parse(args[0]) : 1000;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1;
        bool archetypes = args.Length > 2 && args[2] == "archetypes";

        var clock = Stopwatch.StartNew();
        var report = BalanceSimulation.Run(runs, seed);
        Console.WriteLine(BalanceSimulation.Format(report, $"Seeds {seed}-{seed + runs - 1}"));
        Console.WriteLine($"({clock.Elapsed.TotalSeconds:0.0} s)");

        if (!archetypes) return;
        Console.WriteLine();
        Console.WriteLine("| Focus | Win rate | Reached floor 4 | Avg score |");
        Console.WriteLine("| ----- | -------: | --------------: | --------: |");
        foreach (Archetype a in Enum.GetValues(typeof(Archetype)))
        {
            var r = BalanceSimulation.Run(runs, seed, a);
            Console.WriteLine($"| {a} | {r.WinRate:P1} | {(double)r.Reached[3, 0] / r.Runs:P0} | {r.TotalScore / r.Runs:N0} |");
        }
    }
}
