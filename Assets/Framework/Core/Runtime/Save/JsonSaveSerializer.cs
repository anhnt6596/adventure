using System.IO;
using Newtonsoft.Json;

namespace Core.Save
{
    public sealed class JsonSaveSerializer : ISaveSerializer
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
        };

        public void Serialize(object data, Stream stream)
        {
            using var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(data, Settings));
        }

        public object Deserialize(Stream stream)
        {
            using var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject<SaveBag>(reader.ReadToEnd(), Settings);
        }
    }
}
