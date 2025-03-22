using System.Collections.Concurrent;

namespace CAS
{
    public class ParallelCellularAutomataKernel : ICellularAutomataKernel
    {
        public Cell[,] Grid { get; private set; }
        private LocalWeather[,] weatherGrid;
        private readonly Random random = new Random();

        private object[] cellLocks;

        public void LoadInitialState(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            int rowCount = lines.Length;
            int colCount = lines[0].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

            Grid = new Cell[rowCount, colCount];
            weatherGrid = new LocalWeather[rowCount, colCount];

            int numLocks = Environment.ProcessorCount * 2;
            cellLocks = new object[numLocks];
            for (int i = 0; i < numLocks; i++)
            {
                cellLocks[i] = new object();
            }

            for (int row = 0; row < rowCount; row++)
            {
                string[] tokens = lines[row].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
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
            {
                condition = WeatherCondition.Rain;
            }
            else if (r < SimulationConstants.RAIN_PROB + SimulationConstants.DROUGHT_PROB)
            {
                condition = WeatherCondition.Drought;
            }
            else
            {
                condition = WeatherCondition.Normal;
            }
            int duration = random.Next(SimulationConstants.MIN_WEATHER_DURATION, SimulationConstants.MAX_WEATHER_DURATION + 1);
            return new LocalWeather(condition, duration);
        }

        private void UpdateLocalWeather()
        {
            int rows = weatherGrid.GetLength(0);
            int cols = weatherGrid.GetLength(1);
            Parallel.ForEach(Partitioner.Create(0, rows), range =>
            {
                for (int row = range.Item1; row < range.Item2; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        LocalWeather currentWeather = weatherGrid[row, col];
                        currentWeather.Duration--;
                        if (currentWeather.Duration <= 0)
                        {
                            currentWeather = GenerateRandomLocalWeather();
                        }
                        weatherGrid[row, col] = currentWeather;
                    }
                }
            });
        }

        private int CountPlantNeighbors(int centerRow, int centerCol)
        {
            int plantNeighborCount = 0;
            int totalRows = Grid.GetLength(0);
            int totalCols = Grid.GetLength(1);
            for (int currentRow = centerRow - 1; currentRow <= centerRow + 1; currentRow++)
            {
                for (int currentCol = centerCol - 1; currentCol <= centerCol + 1; currentCol++)
                {
                    bool isCenterCell = (currentRow == centerRow && currentCol == centerCol);
                    bool isWithinBounds = (currentRow >= 0 && currentRow < totalRows && currentCol >= 0 && currentCol < totalCols);
                    if (!isCenterCell && isWithinBounds)
                    {
                        if (Grid[currentRow, currentCol].State == CellState.Plant)
                        {
                            plantNeighborCount++;
                        }
                    }
                }
            }
            return plantNeighborCount;
        }

        private List<Position> GetNeighborPositions(int centerRow, int centerCol)
        {
            int totalRows = Grid.GetLength(0);
            int totalCols = Grid.GetLength(1);
            var neighborPositions = new List<Position>();
            for (int deltaRow = -1; deltaRow <= 1; deltaRow++)
            {
                for (int deltaCol = -1; deltaCol <= 1; deltaCol++)
                {
                    bool isCenterCell = (deltaRow == 0 && deltaCol == 0);
                    int neighborRow = centerRow + deltaRow;
                    int neighborCol = centerCol + deltaCol;
                    bool isWithinBounds = (neighborRow >= 0 && neighborRow < totalRows && neighborCol >= 0 && neighborCol < totalCols);
                    if (!isCenterCell && isWithinBounds)
                    {
                        neighborPositions.Add(new Position(neighborRow, neighborCol));
                    }
                }
            }
            return neighborPositions;
        }

        private int GetLockIndex(int row, int col, int totalCols)
        {
            return ((row * totalCols) + col) % cellLocks.Length;
        }

        private void ProcessAnimalBehavior(int row, int col, bool[,] processed, Cell[,] nextGrid)
        {
            Cell animalCell = Grid[row, col];
            CellState animalType = animalCell.State;
            int energy = animalCell.Energy;
            processed[row, col] = true;

            List<Position> neighbors = GetNeighborPositions(row, col);
            neighbors = neighbors.OrderBy(n => random.Next()).ToList();
            bool hasFed = false;
            Position targetPosition = new Position(row, col);
            int cols = Grid.GetLength(1);

            if (animalType == CellState.Herbivore)
            {
                var plantCandidates = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Plant).ToList();
                if (plantCandidates.Any())
                {
                    targetPosition = plantCandidates.First();
                    energy += SimulationConstants.ENERGY_GAIN_FROM_PLANT;
                    hasFed = true;
                }
            }
            else if (animalType == CellState.Carnivore)
            {
                var preyCandidates = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Herbivore ||
                                                              Grid[pos.Row, pos.Col].State == CellState.Omnivore).ToList();
                if (preyCandidates.Any())
                {
                    targetPosition = preyCandidates.First();
                    energy += CarnivoreConstants.ENERGY_GAIN_FROM_ATTACK;
                    hasFed = true;
                }
            }
            else if (animalType == CellState.Omnivore)
            {
                var plantCandidates = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Plant).ToList();
                if (plantCandidates.Any())
                {
                    targetPosition = plantCandidates.First();
                    energy += SimulationConstants.ENERGY_GAIN_FROM_PLANT;
                    hasFed = true;
                }
                if (!hasFed && energy < SimulationConstants.HUNGER_THRESHOLD)
                {
                    var herbivoreCandidates = neighbors.Where(pos => Grid[pos.Row, pos.Col].State == CellState.Herbivore).ToList();
                    if (herbivoreCandidates.Any())
                    {
                        targetPosition = herbivoreCandidates.First();
                        energy += SimulationConstants.ENERGY_GAIN_FROM_ATTACK;
                        hasFed = true;
                    }
                }
            }

            int targetLockIndex = GetLockIndex(targetPosition.Row, targetPosition.Col, cols);
            if (hasFed)
            {
                Cell newCellForTarget = new Cell(animalType, energy);
                lock (cellLocks[targetLockIndex])
                {
                    nextGrid[targetPosition.Row, targetPosition.Col] = newCellForTarget;
                }
                if (targetPosition.Row != row || targetPosition.Col != col)
                {
                    int originLockIndex = GetLockIndex(row, col, cols);
                    lock (cellLocks[originLockIndex])
                    {
                        nextGrid[row, col] = new Cell(CellState.Empty);
                    }
                }
            }
            else
            {
                var emptyPositions = neighbors.Where(pos => nextGrid[pos.Row, pos.Col].State == CellState.Empty).ToList();
                if (emptyPositions.Any())
                {
                    targetPosition = emptyPositions.First();
                    WeatherCondition destWeather = weatherGrid[targetPosition.Row, targetPosition.Col].Condition;
                    int effectiveLoss = SimulationConstants.ENERGY_LOSS_PER_MOVE;
                    if (destWeather == WeatherCondition.Drought)
                        effectiveLoss += 1;
                    else if (destWeather == WeatherCondition.Rain)
                        effectiveLoss = Math.Max(1, SimulationConstants.ENERGY_LOSS_PER_MOVE - 1);
                    energy -= effectiveLoss;
                    int destLockIndex = GetLockIndex(targetPosition.Row, targetPosition.Col, cols);
                    Cell newCellForTarget = new Cell(animalType, energy);
                    lock (cellLocks[destLockIndex])
                    {
                        nextGrid[targetPosition.Row, targetPosition.Col] = newCellForTarget;
                    }
                    if (targetPosition.Row != row || targetPosition.Col != col)
                    {
                        int originLockIndex = GetLockIndex(row, col, cols);
                        lock (cellLocks[originLockIndex])
                        {
                            nextGrid[row, col] = new Cell(CellState.Empty);
                        }
                    }
                }
                else
                {
                    WeatherCondition currentWeather = weatherGrid[row, col].Condition;
                    int effectiveLoss = SimulationConstants.ENERGY_LOSS_PER_MOVE;
                    if (currentWeather == WeatherCondition.Drought)
                        effectiveLoss += 1;
                    else if (currentWeather == WeatherCondition.Rain)
                        effectiveLoss = Math.Max(1, SimulationConstants.ENERGY_LOSS_PER_MOVE - 1);
                    energy -= effectiveLoss;
                    int originLockIndex = GetLockIndex(row, col, cols);
                    Cell newCellAtOrigin = new Cell(animalType, energy);
                    lock (cellLocks[originLockIndex])
                    {
                        nextGrid[row, col] = newCellAtOrigin;
                    }
                }
            }

            int reproductionThreshold = animalType == CellState.Carnivore ? CarnivoreConstants.REPRODUCTION_THRESHOLD : SimulationConstants.REPRODUCTION_THRESHOLD;
            int reproductionCost = animalType == CellState.Carnivore ? CarnivoreConstants.REPRODUCTION_COST : SimulationConstants.REPRODUCTION_COST;

            if (energy >= reproductionThreshold)
            {
                List<Position> reproductionCandidates = neighbors.Where(pos => Grid[pos.Row, pos.Col].State != CellState.Herbivore &&
                                                                                Grid[pos.Row, pos.Col].State != CellState.Carnivore &&
                                                                                Grid[pos.Row, pos.Col].State != CellState.Omnivore).ToList();
                if (reproductionCandidates.Any() && random.NextDouble() < 0.5)
                {
                    Position repPos = reproductionCandidates.First();
                    int repLockIndex = GetLockIndex(repPos.Row, repPos.Col, cols);
                    Cell newOffspring = new Cell(animalType,
                        animalType == CellState.Carnivore ? CarnivoreConstants.INITIAL_ENERGY : SimulationConstants.INITIAL_ANIMAL_ENERGY);
                    lock (cellLocks[repLockIndex])
                    {
                        nextGrid[repPos.Row, repPos.Col] = newOffspring;
                    }
                    energy -= reproductionCost;
                    int targetLockIndexAfter = GetLockIndex(targetPosition.Row, targetPosition.Col, cols);
                    Cell updatedParent = new Cell(animalType, energy);
                    lock (cellLocks[targetLockIndexAfter])
                    {
                        nextGrid[targetPosition.Row, targetPosition.Col] = updatedParent;
                    }
                }
            }
        }

        public void Step()
        {
            int rows = Grid.GetLength(0);
            int cols = Grid.GetLength(1);

            // Phase 0: Update local weather.
            UpdateLocalWeather();

            // Phase 1: Process plants.
            Cell[,] nextGrid = new Cell[rows, cols];
            Parallel.ForEach(Partitioner.Create(0, rows), range =>
            {
                for (int row = range.Item1; row < range.Item2; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        Cell currentCell = Grid[row, col];
                        if (currentCell.State == CellState.Plant)
                        {
                            int plantNeighbors = CountPlantNeighbors(row, col);
                            nextGrid[row, col] = (plantNeighbors > 4)
                                ? new Cell(CellState.Empty)
                                : new Cell(CellState.Plant);
                        }
                        else if (currentCell.State == CellState.Empty)
                        {
                            int plantNeighbors = CountPlantNeighbors(row, col);
                            nextGrid[row, col] = (plantNeighbors >= 2)
                                ? new Cell(CellState.Plant)
                                : new Cell(CellState.Empty);
                        }
                        else
                        {
                            nextGrid[row, col] = currentCell;
                        }
                    }
                }
            });

            // Phase 1a: Apply environmental effects on plants.
            Parallel.ForEach(Partitioner.Create(0, rows), range =>
            {
                for (int row = range.Item1; row < range.Item2; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        if (nextGrid[row, col].State == CellState.Plant)
                        {
                            WeatherCondition localWeather = weatherGrid[row, col].Condition;
                            if (localWeather == WeatherCondition.Rain && random.NextDouble() < SimulationConstants.RAIN_PLANT_BIRTH_CHANCE)
                            {
                                nextGrid[row, col] = new Cell(CellState.Plant);
                            }
                            if (localWeather == WeatherCondition.Drought && random.NextDouble() < SimulationConstants.DROUGHT_PLANT_DEATH_CHANCE)
                            {
                                nextGrid[row, col] = new Cell(CellState.Empty);
                            }
                        }
                    }
                }
            });

            // Phase 2: Process animal behavior.
            bool[,] processed = new bool[rows, cols];
            Parallel.ForEach(Partitioner.Create(0, rows), range =>
            {
                for (int row = range.Item1; row < range.Item2; row++)
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
            });

            // Phase 3: Kill animals with energy <= 0.
            Parallel.ForEach(Partitioner.Create(0, rows), range =>
            {
                for (int row = range.Item1; row < range.Item2; row++)
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
            });

            Grid = nextGrid;
        }

        public LocalWeather[,] GetWeatherGrid()
        {
            return weatherGrid;
        }
    }
}
