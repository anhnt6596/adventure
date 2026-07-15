using System.Collections;
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
        var offset = (new Vector3(0.5f, 0, 0.5f)) * cellSize + gridMono.transform.position;
        int count = 0;
        for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Height; y++)
            {
                if (!grid.IsEmpty(x, y)) continue;
                count++;
                SpriteRenderer sq;
                if (count >= squares.Count)
                {
                    sq = Instantiate(squareRoot, transform);
                    squares.Add(sq);
                }
                else sq = squares[count];
                sq.gameObject.SetActive(true);
                sq.transform.localScale = Vector3.one * cellSize * 0.96f;
                sq.transform.localPosition = new Vector3(x * cellSize, 0, y * cellSize) + offset;
            }
        {
        }
    }
}
