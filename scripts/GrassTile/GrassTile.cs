using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tile
{
    public Terrain terrain;  //tile所在的地形
    public Bounds bounds; //用于剔除
    public Vector2Int gridPosition;  //在terrain上的位置

    public Tile(Terrain t, Bounds b, Vector2Int pos)
    {
        terrain = t;
        bounds = b;
        gridPosition = pos;

    }
}
