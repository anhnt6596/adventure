using System.IO;

namespace Core.Save
{
    public interface ISaveSerializer
    {
        void Serialize(object data, Stream stream);
        object Deserialize(Stream stream);
    }
}
