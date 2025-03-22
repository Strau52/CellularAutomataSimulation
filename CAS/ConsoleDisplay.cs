namespace CAS
{
    public static class ConsoleDisplay
    {
        public static void Show(Cell[,] grid, LocalWeather[,] weatherGrid, int generation)
        {
            int rows = grid.GetLength(0);
            int cols = grid.GetLength(1);
            Console.Clear();
            Console.WriteLine($"Generation: {generation}");
            Console.WriteLine();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    switch (weatherGrid[i, j].Condition)
                    {
                        case WeatherCondition.Rain:
                            Console.BackgroundColor = ConsoleColor.Blue;
                            break;
                        case WeatherCondition.Drought:
                            Console.BackgroundColor = ConsoleColor.DarkYellow;
                            break;
                        default:
                            Console.BackgroundColor = ConsoleColor.Black;
                            break;
                    }

                    char symbol = ' ';
                    switch (grid[i, j].State)
                    {
                        case CellState.Plant:
                            symbol = '■';
                            break;
                        case CellState.Herbivore:
                            symbol = 'H';
                            break;
                        case CellState.Carnivore:
                            symbol = 'C';
                            break;
                        case CellState.Omnivore:
                            symbol = 'O';
                            break;
                        default:
                            symbol = ' ';
                            break;
                    }
                    Console.Write(symbol);
                    Console.BackgroundColor = ConsoleColor.Black;
                }
                Console.WriteLine();
            }
        }
    }
}