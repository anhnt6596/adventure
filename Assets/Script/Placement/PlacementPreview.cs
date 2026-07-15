using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlacementPreview : MonoBehaviour
{
    [SerializeField] Color avaiableColor, unavaiableColor;
    [SerializeField] SpriteRenderer squareRoot;
    List<SpriteRenderer> squares = new List<SpriteRenderer>();

    private void Awake()
    {
        squares.Add(squareRoot);
    }

    public void Display(GridSize size, float cellSize = 1)
    {
        squares.ForEach(s => s.gameObject.SetActive(false));

        var s = size.w * size.h;
        var offset = GridMath.GetOffsetXZ(size, cellSize);
        for (int i = 0; i < s; i++)
        {
            SpriteRenderer sq;
            if (i >= squares.Count)
            {
                sq = Instantiate(squareRoot, transform);
                squares.Add(sq);
            }
            else sq = squares[i];
            var x = i % size.w;
            var y = i / size.w;   // row = index / width (was size.h -> wrong for non-square footprints)
            sq.gameObject.SetActive(true);
            sq.transform.localScale = Vector3.one * cellSize * 0.96f;
            sq.transform.localPosition = new Vector3(x * cellSize, 0, y * cellSize) - offset;
        }
    }

    public void ShowAvaiable(bool isAvaiable)
    {
        squares.ForEach(s => s.color = isAvaiable ? avaiableColor : unavaiableColor);
    }
}
