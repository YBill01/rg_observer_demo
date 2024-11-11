using System;
using Unity.Mathematics;
using UnityEngine;

using UnityEditor;
using Legacy.Database;

namespace Legacy.Observer
{
    public class EnumBitFieldAttribute : PropertyAttribute
	{
		public EnumBitFieldAttribute(Type enumType)
		{
			this.enumType = enumType;
		}
		public Type enumType;
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(EnumBitFieldAttribute))]
	public class EnumBitFieldAttributeDrawer : PropertyDrawer
    {
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var _attribute = attribute as EnumBitFieldAttribute;
			var names = Enum.GetNames(_attribute.enumType);
			property.intValue = EditorGUI.MaskField(position, label, property.intValue, names);
		}
	}
#endif

	public class TileBehaviour : MonoBehaviour
    {
		public TileStatus Status = TileStatus.Empty;
		[EnumBitField(typeof(MinionLayerType))] public byte Layer;

		/*public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var _tile_size = ServerSettings.TileSize.FloatValue;
			var _position = new float2(transform.position.x, transform.position.z);
			var _int_position = TileData.Float2Int2Tile(_position, _tile_size);

			dstManager.AddComponentData(entity, new TileData
			{
				index = _int_position,
				position = _position,
				status = Status,
				layer = Layer
			});
		}*/

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
            ConfigVar.Init();
			switch (Status)
			{
				case TileStatus.Empty:
					Gizmos.color = Color.green;
					break;
				case TileStatus.Blocked:
					Gizmos.color = Color.black;
					break;
				case TileStatus.Waypoint:
					Gizmos.color = Color.blue;
					break;
				case TileStatus.BridgeUp:
				case TileStatus.BridgeDown:
					Gizmos.color = Color.cyan;
					break;
			}
			//UnityEngine.Debug.Log(ServerSettings.TileSize);
			Gizmos.DrawWireCube(transform.position, new float3(GetComponentInParent<GridBehaviour>().TileSize - 0.08f, .005f, GetComponentInParent<GridBehaviour>().TileSize - 0.08f));
		}
#endif
	}
}
