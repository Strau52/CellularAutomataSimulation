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
            ICellularAutomataKernel? selectedKernel = null;

            bool isBenchmarkMode = PromptBenchmarkMode();

            if (!isBenchmarkMode)
            {
                string kernelChoice = PromptKernelMode();
                selectedKernel = kernelChoice.Equals("sequential", StringComparison.OrdinalIgnoreCase) ? sequentialKernel : parallelKernel;
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
                    selectedKernel = sequentialKernel;
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

                List<long> sequentialRunTimes = MeasurePerformance(sequentialKernel, selectedFilePath, maxGenerations, numRuns);
                List<long> parallelRunTimes = MeasurePerformance(parallelKernel, selectedFilePath, maxGenerations, numRuns);

                double avgSequential = sequentialRunTimes.Average();
                double avgParallel = parallelRunTimes.Average();
                long medianSequential = CalculateMedian(sequentialRunTimes);
                long medianParallel = CalculateMedian(parallelRunTimes);

                double avgSpeedupPercent = (avgSequential / avgParallel - 1) * 100;
                double medianSpeedupPercent = (medianSequential / (double)medianParallel - 1) * 100;

                int consoleWidth = Console.WindowWidth;
                string border = new string('=', consoleWidth);
                string title = "RESULTS";
                string centeredTitle = new string(' ', (consoleWidth - title.Length) / 2) + title;

                Console.WriteLine(border);
                Console.WriteLine(centeredTitle);
                Console.WriteLine(border);
                Console.WriteLine($"The grid was {selectedKernel.Grid.GetLength(0)}x{selectedKernel.Grid.GetLength(1)}");
                Console.WriteLine($"Average sequential solution ({maxGenerations} generations): {avgSequential:0.##} ms in {numRuns} runs");
                Console.WriteLine($"Average parallel solution ({maxGenerations} generations): {avgParallel:0.##} ms in {numRuns} runs");
                Console.WriteLine($"Median sequential solution: {medianSequential} ms");
                Console.WriteLine($"Median parallel solution: {medianParallel} ms");
                Console.WriteLine($"Average speedup: {avgSpeedupPercent:0.##} %");
                Console.WriteLine($"Median speedup: {medianSpeedupPercent:0.##} %");
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
                Console.Write("Enter S for sequential or P for parallel: ");
                string input = Console.ReadLine().Trim().ToLower();
                if (input == "s" || input == "sequential")
                {
                    return "sequential";
                }
                else if (input == "p" || input == "parallel")
                {
                    return "parallel";
                }
                Console.WriteLine("Invalid input. Please enter S or P.");
            }
        }

        private static bool PromptBenchmarkMode()
        {
            while (true)
            {
                Console.Write("Run in benchmark mode? (T/F): ");
                string input = Console.ReadLine().Trim().ToUpper();
                if (input == "T" || input == "TRUE")
                {
                    return true;
                }
                else if (input == "F" || input == "FALSE")
                {
                    return false;
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter T for true or F for false.");
                }
            }
        }

        private static int PromptPositiveInteger(string message)
        {
            while (true)
            {
                Console.Write(message);
                if (int.TryParse(Console.ReadLine(), out int value) && value > 0)
                {
                    return value;
                }
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
                LocalWeather[,] weatherGrid = (kernel is SequentialCellularAutomataKernel seqKernel) ? seqKernel.GetWeatherGrid() : ((ParallelCellularAutomataKernel)kernel).GetWeatherGrid();
                ConsoleDisplay.Show(kernel.Grid, weatherGrid, generation);
                generation++;
                Thread.Sleep(500);
                kernel.Step();
            }
        }
    }
}
