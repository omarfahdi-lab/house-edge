using System;
using System.Diagnostics;
using HouseEdge.Core;

// dotnet run -c Release -- [runs] [first seed] [archetypes|stakes]
// "archetypes" also plays one batch per archetype focus, "stakes" one batch per stake
class Program
{
    static void Main(string[] args)
    {
        int runs = args.Length > 0 ? int.Parse(args[0]) : 1000;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1;
        bool archetypes = args.Length > 2 && args[2] == "archetypes";
        bool stakes = args.Length > 2 && args[2] == "stakes";

        var clock = Stopwatch.StartNew();
        var report = BalanceSimulation.Run(runs, seed);
        Console.WriteLine(BalanceSimulation.Format(report, $"Seeds {seed}-{seed + runs - 1}"));
        Console.WriteLine($"({clock.Elapsed.TotalSeconds:0.0} s)");

        if (stakes)
        {
            Console.WriteLine();
            Console.WriteLine("| Stake | Win rate | Reached floor 4 | Reached floor 8 |");
            Console.WriteLine("| ----- | -------: | --------------: | --------------: |");
            foreach (Stake stake in Enum.GetValues(typeof(Stake)))
            {
                var r = BalanceSimulation.Run(runs, seed, options: new RunOptions { Stake = stake });
                Console.WriteLine($"| {stake} | {r.WinRate:P1} | {(double)r.Reached[3, 0] / r.Runs:P0} | {(double)r.Reached[7, 0] / r.Runs:P0} |");
            }
            return;
        }
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
