using System.Collections.Generic;

using UnityEngine;
using Unity.Mathematics;
using UnityEditor;
using System.IO;

namespace Legacy.Observer
{

    //https://www.redblobgames.com/grids/hexagons/#range

    public class GridBehaviour : MonoBehaviour
    {

        public GameObject TilePrefab;
        public int MapWidth;
        public int MapHeight;
        public float TileSize;
        public void Generate()
        {
#if UNITY_EDITOR
            ClearGrid();
            int3 _Index = int3.zero;
            // поменять тайл сайз в  ObserverSettings.TileSize.FloatValue!!!!!!!!!!!!!!!!!!!!!!!!
            //поменяла
            var _tile_size = TileSize;
            for (int z = -MapHeight; z < MapHeight; z++)
            {
                for (int x = -MapWidth; x < MapWidth; x++)
                {
                    var _tile_prefab = (GameObject)PrefabUtility.InstantiatePrefab(TilePrefab, transform);
                    _tile_prefab.name = string.Format("Tile[{0},{1}]", x, z);
                    _tile_prefab.transform.position = new float3(x * _tile_size + _tile_size * 0.5f, transform.position.y, z * _tile_size + _tile_size * 0.5f);
                }
            }
#endif
        }

        //        public void SaveBattleGrid()
        //        {
        //#if UNITY_EDITOR
        //            using (var binary = new BinaryWriter(new FileStream("Packages/legacy.server/Resources/grid.dat", FileMode.Create)))
        //            {
        //                var _tiles = transform.GetComponentsInChildren<TileBehaviour>();
        //                //binary.Write()
        //                for (int i = 0; i < _tiles.Length; ++i)
        //                {
        //                    var _tile = _tiles[i];

        //                }
        //                UnityEngine.Debug.Log($"tiles: {_tiles.Length}");
        //            }
        //#endif
        //        }

        public void ClearGrid()
        {
            UnityEngine.Debug.Log("Clearing grid...");
            Transform[] _Children = GetComponentsInChildren<Transform>();
            List<GameObject> _Removed = new List<GameObject>();
            foreach (Transform _child in _Children)
            {
                if (_child != transform)
                {
                    _Removed.Add(_child.gameObject);
                }
            }
            while (_Removed.Count > 0)
            {
                DestroyImmediate(_Removed[0], false);
                _Removed.RemoveAt(0);
            }
        }
    }
}