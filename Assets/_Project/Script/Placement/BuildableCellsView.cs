using System.Collections.Generic;
using UnityEngine;

public class BuildableCellsView : MonoBehaviour
{
    [SerializeField] SpriteRenderer squareRoot;
    [SerializeField] MonoBehaviour gridMono;
    List<SpriteRenderer> squares = new List<SpriteRenderer>();
    IGrid grid;

    private void Awake()
    {
        squares.Add(squareRoot);
        grid = gridMono.GetComponent<IGrid>();
    }

    public void Display()
    {
        squares.ForEach(s => s.gameObject.SetActive(false));

        var cellSize = grid.CellSize;
        var origin = gridMono.transform.position + new Vector3(0.5f, 0, 0.5f) * cellSize;

        int i = 0;
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                if (!grid.IsEmpty(x, y)) continue;

                SpriteRenderer sq;
                if (i >= squares.Count)
                {
                    sq = Instantiate(squareRoot, transform);
                    squares.Add(sq);
                }
                else sq = squares[i];

                sq.gameObject.SetActive(true);
                sq.transform.localScale = Vector3.one * cellSize * 0.96f;
                sq.transform.position = origin + new Vector3(x * cellSize, 0, y * cellSize);
                i++;
            }
    }
}
