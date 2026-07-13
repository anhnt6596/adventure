using Cysharp.Threading.Tasks;

namespace Core
{
    public static class AwaitableExtensions
    {
        public static UniTask<T> WaitForResult<T>(this IAwaitable<T> self)
        {
            self.ResultSource = new UniTaskCompletionSource<T>();
            return self.ResultSource.Task;
        }

        public static void SetResult<T>(this IAwaitable<T> self, T value)
        {
            self.ResultSource?.TrySetResult(value);
            self.ResultSource = null;
        }
    }
}
