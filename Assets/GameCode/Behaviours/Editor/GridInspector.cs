using UnityEditor;
using UnityEngine;

namespace Legacy.Observer
{
    [CustomEditor(typeof(GridBehaviour))]

    public class GridInspector : Editor
    {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
			var _grid = target as GridBehaviour;

            if (GUILayout.Button("Generate Battle"))
				_grid.Generate();

            //if (GUILayout.Button("Save Battle Grid"))
            //    _grid.SaveBattleGrid();
        }
    }
}