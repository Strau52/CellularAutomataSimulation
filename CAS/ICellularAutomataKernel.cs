namespace CAS
{
    public interface ICellularAutomataKernel
    {
        Cell[,] Grid { get; }
        void LoadInitialState(string filePath);
        void Step();
    }
}
