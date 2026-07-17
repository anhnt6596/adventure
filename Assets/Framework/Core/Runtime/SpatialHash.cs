using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    // "Which items are near this point" as a uniform grid. A data structure, not a system: each
    // caller owns one, so nothing that only collides ends up in a combat query.
    public class SpatialHash<T>
    {
        readonly List<T> _items = new List<T>();
        readonly Func<T, Vector3> _positionOf;
        readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>();
        readonly Stack<List<int>> _pool = new Stack<List<int>>();

        readonly float _cellSize;

        // Pairs further apart than one cell are never compared, so the cell must exceed the largest
        // query or contact distance in use.
        public SpatialHash(Func<T, Vector3> positionOf, float cellSize)
        {
            _positionOf = positionOf;
            _cellSize = Mathf.Max(0.01f, cellSize);
        }

        public IReadOnlyList<T> Items => _items;
        public float CellSize => _cellSize;

        public void Add(T item)
        {
            if (!_items.Contains(item)) _items.Add(item);
        }

        public void Remove(T item) => _items.Remove(item);

        public void Rebuild()
        {
            foreach (var list in _buckets.Values)
            {
                list.Clear();
                _pool.Push(list);
            }
            _buckets.Clear();

            for (int i = 0; i < _items.Count; i++)
                BucketOf(_positionOf(_items[i])).Add(i);
        }

        public void Query(Vector3 centre, float radius, List<T> results)
        {
            results.Clear();

            int x0 = Mathf.FloorToInt((centre.x - radius) / _cellSize);
            int x1 = Mathf.FloorToInt((centre.x + radius) / _cellSize);
            int z0 = Mathf.FloorToInt((centre.z - radius) / _cellSize);
            int z1 = Mathf.FloorToInt((centre.z + radius) / _cellSize);

            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!_buckets.TryGetValue(Key(x, z), out var list)) continue;
                    for (int i = 0; i < list.Count; i++) results.Add(_items[list[i]]);
                }
        }

        // Each pair is visited once: a bucket sees itself, then only the four buckets ahead of it.
        public void ForEachPair(Action<T, T> action)
        {
            foreach (var cell in _buckets)
            {
                var list = cell.Value;
                for (int a = 0; a < list.Count; a++)
                    for (int b = a + 1; b < list.Count; b++)
                        action(_items[list[a]], _items[list[b]]);

                int cx = (int)(cell.Key >> 32), cz = (int)(cell.Key & 0xFFFFFFFF);
                PairAgainst(list, cx + 1, cz, action);
                PairAgainst(list, cx, cz + 1, action);
                PairAgainst(list, cx + 1, cz + 1, action);
                PairAgainst(list, cx + 1, cz - 1, action);
            }
        }

        void PairAgainst(List<int> list, int cx, int cz, Action<T, T> action)
        {
            if (!_buckets.TryGetValue(Key(cx, cz), out var other)) return;
            for (int a = 0; a < list.Count; a++)
                for (int b = 0; b < other.Count; b++)
                    action(_items[list[a]], _items[other[b]]);
        }

        List<int> BucketOf(Vector3 p)
        {
            long key = Key(Mathf.FloorToInt(p.x / _cellSize), Mathf.FloorToInt(p.z / _cellSize));
            if (_buckets.TryGetValue(key, out var list)) return list;

            list = _pool.Count > 0 ? _pool.Pop() : new List<int>();
            _buckets[key] = list;
            return list;
        }

        static long Key(int x, int z) => ((long)x << 32) | (uint)z;
    }
}
