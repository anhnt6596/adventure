namespace Core.Save
{
    public interface ISavable
    {
        string SaveKey { get; }
        void Save(SaveBag bag);
        void Load(SaveBag bag);
    }
}
