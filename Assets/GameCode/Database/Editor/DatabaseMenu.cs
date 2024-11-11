using UnityEditor;
using Legacy.Database;
using Legacy.Observer;
using Unity.Entities;

public class BatabaseMenu
{

    [MenuItem("GameLegacy/ValidateReceipt")]
    private static void ValidateReceipt()
    {
        IAPValidator.FakeValidate(out ObserverPlayerPaymentResult paymentResult);
        //paymentResult.player_index = 16;
        //var e = World.DefaultGameObjectInjectionWorld.EntityManager;
        //e.AddComponentData(e.CreateEntity(), paymentResult);
    }

    [MenuItem("GameLegacy/Database/Write")]
	private static void DatabaseWrite()
	{
		BinaryDatabase.Instance.Dispose();
		var _instance = new BinaryDatabaseWriter();
		_instance.Write("mongodb://88.99.198.202:27017", WriteType.All);
	}

	[MenuItem("GameLegacy/Database/Read")]
	private static void DatabaseRead()
	{
		BinaryDatabase.Instance.Dispose();

		BinaryDatabase.Instance.Read(true);
		var _settings = Settings.Instance.Get<BaseGameSettings>();
        for (byte i = 0; i < _settings.cards.length; ++i)
        {
            UnityEngine.Debug.Log($"BaseGameSettings >> Cards >> {i} >> {_settings.cards[i]}");
        }
		BinaryDatabase.Instance.Dispose();
	}

    [MenuItem("GameLegacy/Database/Test")]
    private static void DatabaseTest()
    {
        BinaryDatabase.Instance.Dispose();

        BinaryDatabase.Instance.Read(true);

        var _settings = Settings.Instance.Get<ArenaSettings>();

        if (_settings.RatingBattlefield(1333, out BinaryBattlefields info))
        {

        }

        BinaryDatabase.Instance.Dispose();
    }

    /*private static void _binary_write(__mongo_field field, BsonValue value, BinaryWriter data)
	{
		if (field is __mongo_primitive)
		{
			switch (value.BsonType)
			{
				case BsonType.Boolean:
					((__mongo_primitive)field).__write(value.AsBoolean, data);
					break;

				case BsonType.Double:
					((__mongo_primitive)field).__write(value.AsDouble, data);
					break;

				case BsonType.Int32:
					((__mongo_primitive)field).__write(value.AsInt32, data);
					break;
			}
		} else if (field is __mongo_string)
		{
			((__mongo_string)field).__write(value.AsString, data);
		} else if (field is __mongo_struct)
		{
			var _inner_document = value.AsBsonDocument;
			((__mongo_struct)field).__fields((__mongo_field_info _inner) =>
			{
				if (_inner_document.TryGetValue(_inner.name, out BsonValue _inner_value))
				{
					_binary_write(_inner.field, _inner_value, data);
				}
			});
		} else if (field is __mongo_array)
		{
			switch (value.BsonType)
			{
				case BsonType.Array:
					UnityEngine.Debug.Log("123");
					break;

				case BsonType.Document:
					var _inner_document = value.AsBsonDocument;
					var _elements_count = _inner_document.ElementCount;

					data.Write((byte)_elements_count);

					var _child_field = ((__mongo_array)field)._child_field;
					for (byte i = 0; i < _elements_count; ++i)
					{
						var _element = _inner_document.GetElement(i).Value;
						_binary_write(_child_field, _element, data);
					}
					break;
			}
		} else if (field is __mongo_components)
		{
			var _inner_document = value.AsBsonDocument;
			var _elements_count = _inner_document.ElementCount;
			for (byte i = 0; i < _elements_count; ++i)
			{
				var _element = _inner_document.GetElement(i).Value.AsBsonDocument;
				var _index = _element.GetValue("id").AsInt32;
			}
		}
	}*/


}