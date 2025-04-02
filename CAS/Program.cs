using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CAS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ICellularAutomataKernel sequentialKernel = new SequentialCellularAutomataKernel();
            ICellularAutomataKernel parallelKernel = new ParallelCellularAutomataKernel();
            ICellularAutomataKernel taskKernel = new TaskBasedCellularAutomataKernel();
            ICellularAutomataKernel? selectedKernel = sequentialKernel;

            bool isBenchmarkMode = PromptBenchmarkMode();

            if (!isBenchmarkMode)
            {
                string kernelChoice = PromptKernelMode();
                if (kernelChoice.Equals("sequential", StringComparison.OrdinalIgnoreCase))
                    selectedKernel = sequentialKernel;
                else if (kernelChoice.Equals("parallel", StringComparison.OrdinalIgnoreCase))
                    selectedKernel = parallelKernel;
                else if (kernelChoice.Equals("task", StringComparison.OrdinalIgnoreCase))
                    selectedKernel = taskKernel;
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string initialStatesFolder = Path.Combine(baseDirectory, "..", "..", "..", "initial_states");
            string[] initialStateFiles = Directory.GetFiles(initialStatesFolder, "*.txt")
                .OrderBy(file =>
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    Match match = Regex.Match(name, @"^\d+");
                    return (match.Success && int.TryParse(match.Value, out int number)) ? number : int.MaxValue;
                })
                .ThenBy(file => Path.GetFileName(file))
                .ToArray();

            Console.WriteLine("Select an initial state file from the list below:");
            for (int i = 0; i < initialStateFiles.Length; i++)
            {
                Console.WriteLine($"[{i}] {Path.GetFileNameWithoutExtension(initialStateFiles[i])}");
            }

            int fileIndex;
            Console.Write("Enter the index of the file to use: ");
            while (!int.TryParse(Console.ReadLine(), out fileIndex) || fileIndex < 0 || fileIndex >= initialStateFiles.Length)
            {
                Console.Write("Invalid input. Enter a valid index: ");
            }
            string selectedFilePath = initialStateFiles[fileIndex];
            Console.WriteLine($"Selected file: {Path.GetFileName(selectedFilePath)}");

            try
            {
                if (!isBenchmarkMode)
                {
                    selectedKernel.LoadInitialState(selectedFilePath);
                }
                else
                {
                    sequentialKernel.LoadInitialState(selectedFilePath);
                    parallelKernel.LoadInitialState(selectedFilePath);
                    taskKernel.LoadInitialState(selectedFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error reading initial state: " + ex.Message);
                return;
            }
            Console.Clear();

            if (isBenchmarkMode)
            {
                int maxGenerations = PromptPositiveInteger("Enter the maximum number of generations: ");
                int numRuns = PromptPositiveInteger("Enter the number of runs: ");

                List<long> seqTimes = MeasurePerformance(sequentialKernel, selectedFilePath, maxGenerations, numRuns);
                List<long> parTimes = MeasurePerformance(parallelKernel, selectedFilePath, maxGenerations, numRuns);
                List<long> taskTimes = MeasurePerformance(taskKernel, selectedFilePath, maxGenerations, numRuns);

                double avgSeq = seqTimes.Average();
                double avgPar = parTimes.Average();
                double avgTask = taskTimes.Average();

                long medSeq = CalculateMedian(seqTimes);
                long medPar = CalculateMedian(parTimes);
                long medTask = CalculateMedian(taskTimes);

                double speedupParAvg = (avgSeq / avgPar - 1) * 100;
                double speedupTaskAvg = (avgSeq / avgTask - 1) * 100;
                double speedupParMed = (medSeq / (double)medPar - 1) * 100;
                double speedupTaskMed = (medSeq / (double)medTask - 1) * 100;

                int consoleWidth = Console.WindowWidth;
                string border = new string('=', consoleWidth);
                string title = "RESULTS";
                string centeredTitle = new string(' ', (consoleWidth - title.Length) / 2) + title;

                Console.WriteLine(border);
                Console.WriteLine(centeredTitle);
                Console.WriteLine(border);
                Console.WriteLine($"The grid was {sequentialKernel.Grid.GetLength(0)}x{sequentialKernel.Grid.GetLength(1)}");
                Console.WriteLine();
                Console.WriteLine("Sequential Kernel:");
                Console.WriteLine($"   Average: {avgSeq:0.##} ms, Median: {medSeq} ms");
                Console.WriteLine();
                Console.WriteLine("Parallel Kernel:");
                Console.WriteLine($"   Average: {avgPar:0.##} ms, Median: {medPar} ms, Speedup vs. sequential: {speedupParAvg:0.##}% (avg), {speedupParMed:0.##}% (median)");
                Console.WriteLine();
                Console.WriteLine("Task-based Kernel:");
                Console.WriteLine($"   Average: {avgTask:0.##} ms, Median: {medTask} ms, Speedup vs. sequential: {speedupTaskAvg:0.##}% (avg), {speedupTaskMed:0.##}% (median)");
            }
            else
            {
                RunInteractiveMode(selectedKernel);
            }
        }

        private static string PromptKernelMode()
        {
            while (true)
            {
                Console.Write("Enter S for sequential, P for parallel, or T for task-based: ");
                string input = Console.ReadLine().Trim().ToLower();
                if (input == "s" || input == "sequential")
                    return "sequential";
                if (input == "p" || input == "parallel")
                    return "parallel";
                if (input == "t" || input == "task" || input == "task-based")
                    return "task";
                Console.WriteLine("Invalid input. Please enter S, P, or T.");
            }
        }

        private static bool PromptBenchmarkMode()
        {
            while (true)
            {
                Console.Write("Run in benchmark mode? (T/F): ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (input == "T" || input == "TRUE")
                    return true;
                if (input == "F" || input == "FALSE")
                    return false;
                Console.WriteLine("Invalid input. Please enter T or F.");
            }
        }

        private static int PromptPositiveInteger(string message)
        {
            while (true)
            {
                Console.Write(message);
                if (int.TryParse(Console.ReadLine(), out int value) && value > 0)
                    return value;
                Console.WriteLine("Invalid input. Please enter a positive integer.");
            }
        }

        private static List<long> MeasurePerformance(ICellularAutomataKernel kernel, string filePath, int maxGenerations, int numRuns)
        {
            List<long> runTimes = new List<long>();
            string kernelType = kernel.GetType().Name;
            for (int run = 0; run < numRuns; run++)
            {
                Console.WriteLine($"Starting run {run + 1} for kernel {kernelType}...");
                Stopwatch sw = Stopwatch.StartNew();
                for (int gen = 0; gen < maxGenerations; gen++)
                {
                    kernel.Step();
                }
                sw.Stop();
                long elapsed = sw.ElapsedMilliseconds;
                Console.WriteLine($"Run {run + 1} for kernel {kernelType} took {elapsed} ms.");
                runTimes.Add(elapsed);
                kernel.LoadInitialState(filePath);
            }
            return runTimes;
        }

        private static long CalculateMedian(List<long> times)
        {
            List<long> sorted = times.OrderBy(t => t).ToList();
            int count = sorted.Count;
            return (count % 2 == 0) ? (sorted[count / 2 - 1] + sorted[count / 2]) / 2 : sorted[count / 2];
        }

        private static void RunInteractiveMode(ICellularAutomataKernel kernel)
        {
            int generation = 0;
            while (true)
            {
                LocalWeather[,] weatherGrid = (kernel is SequentialCellularAutomataKernel seqKernel) ? seqKernel.GetWeatherGrid()
                    : ((ParallelCellularAutomataKernel)kernel).GetWeatherGrid();
                ConsoleDisplay.Show(kernel.Grid, weatherGrid, generation);
                generation++;
                Thread.Sleep(500);
                kernel.Step();
            }
        }
    }
}
