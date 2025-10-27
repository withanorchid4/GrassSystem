using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Grass : MonoBehaviour
{
    private struct Tile
    {
        public Terrain terrain;
        public Bounds bounds;
        public Vector2Int gridPosition;

        public Tile(Terrain t, Bounds b, Vector2Int pos)
        {
            terrain = t;
            bounds = b;
            gridPosition = pos;
        }
    }
}
