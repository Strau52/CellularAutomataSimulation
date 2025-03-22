namespace gridgenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            int rows = 500;
            int cols = 500;
            Random rnd = new Random();

            double probabilityEmpty = 0.6;
            double probabilityPlant = 0.2;
            double probabilityHerbivore = 0.15;
            double probabilityCarnivore = 0.10;
            double probabilityOmnivore = 0.05;

            string filePath = $"{rows}x{cols}.txt";

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                for (int row = 0; row < rows; row++)
                {
                    string[] lineValues = new string[cols];
                    for (int col = 0; col < cols; col++)
                    {
                        double sample = rnd.NextDouble();
                        int cellValue;
                        if (sample < probabilityEmpty)
                            cellValue = 0;
                        else if (sample < probabilityEmpty + probabilityPlant)
                            cellValue = 1;
                        else if (sample < probabilityEmpty + probabilityPlant + probabilityHerbivore)
                            cellValue = 2;
                        else if (sample < probabilityEmpty + probabilityPlant + probabilityHerbivore + probabilityCarnivore)
                            cellValue = 3;
                        else
                            cellValue = 4;
                        lineValues[col] = cellValue.ToString();
                    }
                    writer.WriteLine(string.Join(" ", lineValues));
                }
            }
            Console.WriteLine($"Generated a {rows}x{cols} grid and saved it to '{filePath}'.");
        }
    }
}