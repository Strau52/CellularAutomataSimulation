namespace CAS
{
    public struct Cell
    {
        public CellState State;
        public int Energy;

        public Cell(CellState state, int energy = 0)
        {
            State = state;
            Energy = energy;
        }
    }
}
