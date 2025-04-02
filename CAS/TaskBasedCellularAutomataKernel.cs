namespace CAS
{
    public class TaskBasedCellularAutomataKernel : ICellularAutomataKernel
    {
        public Cell[,] Grid { get; private set; }
        private LocalWeather[,] weatherGrid;
        private readonly Random random = new Random();

        public void LoadInitialState(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            int rowCount = lines.Length;
            int colCount = lines[0].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            Grid = new Cell[rowCount, colCount];
            weatherGrid = new LocalWeather[rowCount, colCount];

            for (int row = 0; row < rowCount; row++)
            {
                string[] tokens = lines[row]
                    .Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int col = 0; col < colCount; col++)
                {
                    if (int.TryParse(tokens[col], out int cellValue))
                    {
                        switch (cellValue)
                        {
                            case 0:
                                Grid[row, col] = new Cell(CellState.Empty);
                                break;
                            case 1:
                                Grid[row, col] = new Cell(CellState.Plant);
                                break;
                            case 2:
                                Grid[row, col] = new Cell(CellState.Herbivore, SimulationConstants.INITIAL_ANIMAL_ENERGY);
                                break;
                            case 3:
                                Grid[row, col] = new Cell(CellState.Carnivore, CarnivoreConstants.INITIAL_ENERGY);
                                break;
                            case 4:
                                Grid[row, col] = new Cell(CellState.Omnivore, SimulationConstants.INITIAL_ANIMAL_ENERGY);
                                break;
                            default:
                                Grid[row, col] = new Cell(CellState.Empty);
                                break;
                        }
                    }
                    else
                    {
                        Grid[row, col] = new Cell(CellState.Empty);
                    }
                    weatherGrid[row, col] = GenerateRandomLocalWeather();
                }
            }
        }

        private LocalWeather GenerateRandomLocalWeather()
        {
            double r = random.NextDouble();
            WeatherCondition condition;
            if (r < SimulationConstants.RAIN_PROB)
                condition = WeatherCondition.Rain;
            else if (r < SimulationConstants.RAIN_PROB + SimulationConstants.DROUGHT_PROB)
                condition = WeatherCondition.Drought;
            else
                condition = WeatherCondition.Normal;
            int duration = random.Next(SimulationConstants.MIN_WEATHER_DURATION, SimulationConstants.MAX_WEATHER_DURATION + 1);
            return new LocalWeather(condition, duration);
        }

        private void UpdateLocalWeather()
        {
            int rows = weatherGrid.GetLength(0);
            int cols = weatherGrid.GetLength(1);
            int numTasks = Environment.ProcessorCount;
            int chunkSize = (rows + numTasks - 1) / numTasks;
            List<Task> tasks = new List<Task>();

            for (int t = 0; t < numTasks; t++)
            {
                int startRow = t * chunkSize;
                int endRow = Math.Min(rows, startRow + chunkSize);
                tasks.Add(Task.Run(() =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            LocalWeather current = weatherGrid[row, col];
                            current.Duration--;
                            if (current.Duration <= 0)
                            {
                                current = GenerateRandomLocalWeather();
                            }
                            weatherGrid[row, col] = current;
                        }
                    }
                }));
            }
            Task.WaitAll(tasks.ToArray());
        }

        private int CountPlantNeighbors(int centerRow, int centerCol)
        {
            int count = 0;
            int totalRows = Grid.GetLength(0);
            int totalCols = Grid.GetLength(1);

            for (int row = centerRow - 1; row <= centerRow + 1; row++)
            {
                for (int col = centerCol - 1; col <= centerCol + 1; col++)
                {
                    bool isCenter = (row == centerRow && col == centerCol);
                    bool withinBounds = (row >= 0 && row < totalRows && col >= 0 && col < totalCols);
                    if (!isCenter && withinBounds && Grid[row, col].State == CellState.Plant)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private List<Position> GetNeighborPositions(int centerRow, int centerCol)
        {
            int totalRows = Grid.GetLength(0);
            int totalCols = Grid.GetLength(1);
            List<Position> positions = new List<Position>();

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    bool isCenter = (dr == 0 && dc == 0);
                    int newRow = centerRow + dr;
                    int newCol = centerCol + dc;
                    bool withinBounds = (newRow >= 0 && newRow < totalRows && newCol >= 0 && newCol < totalCols);
                    if (!isCenter && withinBounds)
                    {
                        positions.Add(new Position(newRow, newCol));
                    }
                }
            }
            return positions;
        }

        private void ProcessAnimalBehavior(int row, int col, bool[,] processed, Cell[,] nextGrid)
        {
            Cell animal = Grid[row, col];
            CellState state = animal.State;
            int energy = animal.Energy;
            processed[row, col] = true;

            List<Position> neighbors = GetNeighborPositions(row, col).OrderBy(x => random.Next()).ToList();
            bool fed = false;
            Position target = new Position(row, col);

            if (state == CellState.Herbivore)
            {
                var plants = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Plant).ToList();
                if (plants.Any())
                {
                    target = plants.First();
                    energy += SimulationConstants.ENERGY_GAIN_FROM_PLANT;
                    fed = true;
                }
            }
            else if (state == CellState.Carnivore)
            {
                var prey = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Herbivore ||
                                                    Grid[pos.Row, pos.Col].State == CellState.Omnivore).ToList();
                if (prey.Any())
                {
                    target = prey.First();
                    energy += CarnivoreConstants.ENERGY_GAIN_FROM_ATTACK;
                    fed = true;
                }
            }
            else if (state == CellState.Omnivore)
            {
                var plants = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Plant).ToList();
                if (plants.Any())
                {
                    target = plants.First();
                    energy += SimulationConstants.ENERGY_GAIN_FROM_PLANT;
                    fed = true;
                }
                if (!fed && energy < SimulationConstants.HUNGER_THRESHOLD)
                {
                    var herbivores = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Herbivore).ToList();
                    if (herbivores.Any())
                    {
                        target = herbivores.First();
                        energy += SimulationConstants.ENERGY_GAIN_FROM_ATTACK;
                        fed = true;
                    }
                }
            }

            int totalCols = Grid.GetLength(1);
            if (fed)
            {
                nextGrid[target.Row, target.Col] = new Cell(state, energy);
                if (target.Row != row || target.Col != col)
                    nextGrid[row, col] = new Cell(CellState.Empty);
            }
            else
            {
                var empties = neighbors.Where(pos => nextGrid[pos.Row, pos.Col].State == CellState.Empty).ToList();
                if (empties.Any())
                {
                    target = empties.First();
                    WeatherCondition weather = weatherGrid[target.Row, target.Col].Condition;
                    int loss = SimulationConstants.ENERGY_LOSS_PER_MOVE;
                    if (weather == WeatherCondition.Drought)
                        loss += 1;
                    else if (weather == WeatherCondition.Rain)
                        loss = Math.Max(1, SimulationConstants.ENERGY_LOSS_PER_MOVE - 1);
                    energy -= loss;
                    nextGrid[target.Row, target.Col] = new Cell(state, energy);
                    if (target.Row != row || target.Col != col)
                        nextGrid[row, col] = new Cell(CellState.Empty);
                }
                else
                {
                    WeatherCondition currentWeather = weatherGrid[row, col].Condition;
                    int loss = SimulationConstants.ENERGY_LOSS_PER_MOVE;
                    if (currentWeather == WeatherCondition.Drought)
                        loss += 1;
                    else if (currentWeather == WeatherCondition.Rain)
                        loss = Math.Max(1, SimulationConstants.ENERGY_LOSS_PER_MOVE - 1);
                    energy -= loss;
                    nextGrid[row, col] = new Cell(state, energy);
                }
            }

            int reproThreshold = (state == CellState.Carnivore) ? CarnivoreConstants.REPRODUCTION_THRESHOLD : SimulationConstants.REPRODUCTION_THRESHOLD;
            int reproCost = (state == CellState.Carnivore) ? CarnivoreConstants.REPRODUCTION_COST : SimulationConstants.REPRODUCTION_COST;
            if (energy >= reproThreshold)
            {
                var reproCandidates = neighbors.Where(pos => Grid[pos.Row, pos.Col].State != CellState.Herbivore &&
                                                              Grid[pos.Row, pos.Col].State != CellState.Carnivore &&
                                                              Grid[pos.Row, pos.Col].State != CellState.Omnivore).ToList();
                if (reproCandidates.Any() && random.NextDouble() < 0.5)
                {
                    Position reproPos = reproCandidates.First();
                    nextGrid[reproPos.Row, reproPos.Col] = new Cell(state, state == CellState.Carnivore ? CarnivoreConstants.INITIAL_ENERGY : SimulationConstants.INITIAL_ANIMAL_ENERGY);
                    energy -= reproCost;
                    nextGrid[target.Row, target.Col] = new Cell(state, energy);
                }
            }
        }

        public void Step()
        {
            int rows = Grid.GetLength(0);
            int cols = Grid.GetLength(1);

            // Phase 0: Update local weather.
            UpdateLocalWeather();

            Cell[,] nextGrid = new Cell[rows, cols];

            // Phase 1: Process plants.
            Task[] plantTasks = CreatePlantProcessingTasks(rows, cols, nextGrid);
            Task.WaitAll(plantTasks);

            // Phase 1a: Apply environmental effects on plants.
            Task[] envTasks = CreateEnvironmentalTasks(rows, cols, nextGrid);
            Task.WaitAll(envTasks);

            // Phase 2: Process animal behavior.
            Task[] animalTasks = CreateAnimalProcessingTasks(rows, cols, nextGrid);
            Task.WaitAll(animalTasks);

            // Phase 3: Kill animals with energy <= 0.
            Task[] cleanupTasks = CreateCleanupTasks(rows, cols, nextGrid);
            Task.WaitAll(cleanupTasks);

            Grid = nextGrid;
        }

        private Task[] CreatePlantProcessingTasks(int rows, int cols, Cell[,] nextGrid)
        {
            int numTasks = Environment.ProcessorCount;
            int chunkSize = (rows + numTasks - 1) / numTasks;
            List<Task> tasks = new List<Task>();

            for (int t = 0; t < numTasks; t++)
            {
                int startRow = t * chunkSize;
                int endRow = Math.Min(rows, startRow + chunkSize);
                tasks.Add(Task.Run(() =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            Cell current = Grid[row, col];
                            if (current.State == CellState.Plant)
                            {
                                int plantNeighbors = CountPlantNeighbors(row, col);
                                nextGrid[row, col] = (plantNeighbors > 4) ? new Cell(CellState.Empty) : new Cell(CellState.Plant);
                            }
                            else if (current.State == CellState.Empty)
                            {
                                int plantNeighbors = CountPlantNeighbors(row, col);
                                nextGrid[row, col] = (plantNeighbors >= 2) ? new Cell(CellState.Plant) : new Cell(CellState.Empty);
                            }
                            else
                            {
                                nextGrid[row, col] = current;
                            }
                        }
                    }
                }));
            }
            return tasks.ToArray();
        }

        private Task[] CreateEnvironmentalTasks(int rows, int cols, Cell[,] nextGrid)
        {
            int numTasks = Environment.ProcessorCount;
            int chunkSize = (rows + numTasks - 1) / numTasks;
            List<Task> tasks = new List<Task>();

            for (int t = 0; t < numTasks; t++)
            {
                int startRow = t * chunkSize;
                int endRow = Math.Min(rows, startRow + chunkSize);
                tasks.Add(Task.Run(() =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            if (nextGrid[row, col].State == CellState.Plant)
                            {
                                WeatherCondition weather = weatherGrid[row, col].Condition;
                                if (weather == WeatherCondition.Rain && random.NextDouble() < SimulationConstants.RAIN_PLANT_BIRTH_CHANCE)
                                    nextGrid[row, col] = new Cell(CellState.Plant);
                                if (weather == WeatherCondition.Drought && random.NextDouble() < SimulationConstants.DROUGHT_PLANT_DEATH_CHANCE)
                                    nextGrid[row, col] = new Cell(CellState.Empty);
                            }
                        }
                    }
                }));
            }
            return tasks.ToArray();
        }

        private Task[] CreateAnimalProcessingTasks(int rows, int cols, Cell[,] nextGrid)
        {
            int numTasks = Environment.ProcessorCount;
            int chunkSize = (rows + numTasks - 1) / numTasks;
            bool[,] processed = new bool[rows, cols];
            List<Task> tasks = new List<Task>();

            for (int t = 0; t < numTasks; t++)
            {
                int startRow = t * chunkSize;
                int endRow = Math.Min(rows, startRow + chunkSize);
                tasks.Add(Task.Run(() =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            if (!processed[row, col] && (Grid[row, col].State == CellState.Herbivore ||
                                                          Grid[row, col].State == CellState.Carnivore ||
                                                          Grid[row, col].State == CellState.Omnivore))
                            {
                                ProcessAnimalBehavior(row, col, processed, nextGrid);
                            }
                        }
                    }
                }));
            }
            return tasks.ToArray();
        }

        private Task[] CreateCleanupTasks(int rows, int cols, Cell[,] nextGrid)
        {
            int numTasks = Environment.ProcessorCount;
            int chunkSize = (rows + numTasks - 1) / numTasks;
            List<Task> tasks = new List<Task>();

            for (int t = 0; t < numTasks; t++)
            {
                int startRow = t * chunkSize;
                int endRow = Math.Min(rows, startRow + chunkSize);
                tasks.Add(Task.Run(() =>
                {
                    for (int row = startRow; row < endRow; row++)
                    {
                        for (int col = 0; col < cols; col++)
                        {
                            if ((nextGrid[row, col].State == CellState.Herbivore ||
                                 nextGrid[row, col].State == CellState.Carnivore ||
                                 nextGrid[row, col].State == CellState.Omnivore) &&
                                nextGrid[row, col].Energy <= 0)
                            {
                                nextGrid[row, col] = new Cell(CellState.Empty);
                            }
                        }
                    }
                }));
            }
            return tasks.ToArray();
        }

        public LocalWeather[,] GetWeatherGrid()
        {
            return weatherGrid;
        }
    }
}
