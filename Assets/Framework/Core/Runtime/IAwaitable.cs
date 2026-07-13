using Cysharp.Threading.Tasks;

namespace Core
{
    public interface IAwaitable<T>
    {
        UniTaskCompletionSource<T> ResultSource { get; set; }
    }
}
