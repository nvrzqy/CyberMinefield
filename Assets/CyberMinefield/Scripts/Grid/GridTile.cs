using UnityEngine;

namespace CyberMinefield.Grid
{
    public sealed class GridTile : MonoBehaviour
    {
        [SerializeField] private int x;
        [SerializeField] private int y;

        public int X => x;
        public int Y => y;
        public Vector2Int Coordinates => new Vector2Int(X, Y);

        public void Initialize(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }
}
