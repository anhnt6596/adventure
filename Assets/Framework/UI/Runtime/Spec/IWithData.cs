using static UnityEngine.GraphicsBuffer;
using System;
using System.Linq;
using System.Reflection;

namespace Core.UI
{
    public interface IWithData<T> : IWithData
    {
        T Data { get; set; }
    }

    public interface IWithData
    {

    }

    public static class IWithDataExtensions
    {
        public static void SetData<T>(this IWithData<T> target, T data)
        {
            target.Data = data;
        }
    }
}