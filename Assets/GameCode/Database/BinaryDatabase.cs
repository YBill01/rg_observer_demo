using System.Reflection;
using System.Collections.Generic;
using System;
using System.IO;

using MongoDB.Bson;
using MongoDB.Driver;

using Unity.Collections.LowLevel.Unsafe;

using Legacy.Network;
using Unity.Mathematics;
using Legacy.Observer;

namespace Legacy.Database
{
	public enum WriteType : int 
	{
		Base = 1 << 0, 
		Grid = 1 << 1,
		All = 3 
	}

	public class BinaryDatabaseWriter
	{
		private readonly string __database;
		private readonly string __databaseGrid;
		private List<string> __log;
		private Dictionary<string, Type> __types;
		private List<string> __idatabase;
		private Dictionary<ushort, string> __linker;

		private readonly BindingFlags __flags;
		private Type __self;

		private Dictionary<string, List<string>> __translate;
		public BinaryDatabaseWriter()
		{
			__database = Path.Combine("../", "_database.dat");
			__databaseGrid = "../_databaseGrid.dat";
			
			__log = new List<string>();

			__types = new Dictionary<string, Type>();
			__idatabase = new List<string>();

			__linker = new Dictionary<ushort, string>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (assembly.ManifestModule.Name.IndexOf("Legacy") == 0)
				{
					var types = new List<Type>(assembly.GetTypes());
					types.Sort((x, y) => x.Name.CompareTo(y.Name));
					foreach (Type type in types)
					{
						if (type.MemberType == MemberTypes.TypeInfo)
						{
							if (!__types.ContainsKey(type.Name))
							{
								__types.Add(type.Name, type);
								if (type.GetInterface(typeof(IDatabase).Name) != null)
								{
									__idatabase.Add(type.Name);
								}
							}
						}
					}
				}
			}
			__flags = BindingFlags.Instance | BindingFlags.NonPublic;
			__self = typeof(BinaryDatabaseWriter);
			
			__translate = new Dictionary<string, List<string>>();
		}

		private void fillTranslate(IMongoDatabase database)
		{
			var sections = database
				.GetCollection<BsonDocument>("admin_sections")
				.Find(new BsonDocument())
				.Project(new BsonDocument
				{
					{"table", 1},
					{"builder", 1}
				})
				.ToList();
			
			BsonDocument section;
			List<string> list;
			
			foreach (var el in sections)
			{
				list = null;
				foreach (var el2 in el["builder"].AsBsonArray)
				{
					section = el2.AsBsonDocument;
					if (section.Contains("locale") && section["locale"].AsBoolean)
					{
						if (list == null)
						{
							list = new List<string>();
						}
						list.Add(section["name"].AsString);
					}
				}
				
				if (list?.Count > 0)
				{
					__translate.Add(el["table"].AsString, list);
				}
			}
		}
		
		private string _translate(string shortTableName, BsonDocument document, string field)
		{
			if (__translate.ContainsKey(shortTableName))
			{
				if (__translate[shortTableName].Contains(field))
				{
					return $"{shortTableName}:{document["_id"].AsInt32}:{field}";
				}
			}
			
			return document[field].AsString;
		}

        private T _bson_generic<T>(string field, BsonDocument document) where T: struct
        {
            if (document.TryGetValue(field, out BsonValue value))
            {
                
                switch (value.BsonType)
                {
                    case BsonType.Double: return (T)Convert.ChangeType(value.AsDouble, typeof(T));
                    case BsonType.Int32: return (T)Convert.ChangeType(value.AsInt32, typeof(T));
                    case BsonType.Boolean: return (T)Convert.ChangeType(value.AsBoolean, typeof(T));
                    case BsonType.String: return (T)Convert.ChangeType(value.AsString, typeof(T));
                }
            }
            return default;
        }

        private void _write_level_field(string field, BsonDocument document, BinaryWriter binary)
        {
            float _value = 0;
            byte _type = 0;
            float _percent = 0;

            if (document.TryGetValue(field, out BsonValue value))
            {
                _value = _bson_generic<float>("value", value.AsBsonDocument);
                _type = _bson_generic<byte>("type", value.AsBsonDocument);
                _percent = _bson_generic<float>("percent", value.AsBsonDocument);
            }

            binary.Write(_value);
            binary.Write(_type);
            binary.Write(_percent);
        }

		private void _linker(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_component");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var cursor = collection.Find(filter);
			var _list = cursor.ToList();

			__log.Add(string.Format("<color=blue>_linker</color>: {0}", _list.Count));
			var _count = 0;
			var _position = binary.BaseStream.Position;
			binary.Write(_count);
			for (int i = 0; i < _list.Count; ++i)
			{
				var component = _list[i];
				var _class_name = component.GetValue("className").ToString();

				// TODO: validate fields ??
				if (__types.TryGetValue(_class_name, out Type type))
				{
					if(__idatabase.Contains(_class_name))
					{
						var _component_index = (ushort)component.GetValue("_id").AsInt32;
						_count++;

						binary.Write(_component_index);
						binary.Write(_class_name);

						__linker.Add(_component_index, _class_name);

						__log.Add(string.Format(" - <color=green>{0}</color>", _class_name));
						continue;
					}
					
				}
				__log.Add(string.Format(" - <color=red>{0}</color>", _class_name));
			}
			binary.BaseStream.Position = _position;
			binary.Write(_count);
			binary.BaseStream.Position = binary.BaseStream.Length;
		}

		private bool _db_autofollow_element(BsonDocument component, BinaryWriter binary)
		{
			if (component.TryGetValue("id", out BsonValue value))
			{
				var _component_index = (ushort)value.AsInt32;
				if (__linker.TryGetValue(_component_index, out string _class_name))
				{
					if (__types.TryGetValue(_class_name, out Type type))
					{
						binary.Write(_component_index);
						var _raw_bytes = (byte[])__self.GetMethod("_address", __flags)
							.MakeGenericMethod(type)
							.Invoke(this, new object[] { __self, component, __flags });
						binary.Write((ushort)_raw_bytes.Length);
						binary.Write(_raw_bytes);

						return true;
					}
				}
			}
			return false;
		}

		private void _db_autofollow(BsonDocument entity, BinaryWriter binary)
		{
			var _componenets = new List<BsonDocument>();
			var _components_bson = entity.GetValue("components");

			// components
			if (_components_bson.BsonType == BsonType.Document)
			{
				var _document = _components_bson.AsBsonDocument;
				for (int k = 0; k < _document.ElementCount; ++k)
				{
					_componenets.Add(_document.GetElement(k).ToBsonDocument().GetValue("Value").AsBsonDocument);
				}
			}

			var _count = 0;
			var _position = binary.BaseStream.Position;
			binary.Write((byte)_count);
			for (int k = 0; k < _componenets.Count; ++k)
			{
				if(_db_autofollow_element(_componenets[k], binary))
				{
					_count++;
				}
			}

			binary.BaseStream.Position = _position;
			binary.Write((byte)_count);
			binary.BaseStream.Position = binary.BaseStream.Length;
		}

		private void _minions(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_entities");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var entities = collection.Find(filter).ToList();


			binary.Write((ushort)entities.Count);

			for (int i = 0; i < entities.Count; ++i)
			{
				var entity = entities[i];
				var _entity_index = (ushort)entity.GetValue("_id").AsInt32;

				binary.Write(_entity_index);
				binary.Write(entity.GetValue("prefab").AsString);
				binary.Write((byte)entity.GetValue("type").AsInt32);
				var _collider = 0.0f;
				if(entity.TryGetValue("collider", out BsonValue value))
				{
					switch (value.BsonType)
					{
						case BsonType.Int32:
							_collider = value.AsInt32;
							break;
						case BsonType.Double:
							_collider = (float)value.AsDouble;
							break;
					}
				}
				binary.Write(_collider);
				binary.Write((ushort)entity.GetValue("mass").AsInt32);
				binary.Write((ushort)entity.GetValue("appearTime").AsInt32);
				binary.Write((ushort)entity.GetValue("chillTime").AsInt32);
				var _taunt = false;
				if (entity.TryGetValue("taunt", out BsonValue taunt))
				{
					_taunt = taunt.AsBoolean;
				}
				binary.Write(_taunt);


				_db_autofollow(entity, binary);				
			}
			__log.Add(string.Format("<color=blue>_entities</color>: {0}", entities.Count));
		}

		private void _effects(IMongoDatabase database, BinaryWriter binary)
		{

			var collection = database.GetCollection<BsonDocument>("admin_effects");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			__log.Add(string.Format("<color=blue>_effects</color>: {0}", items.Count));

			binary.Write((ushort)items.Count);

			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				var _id = (ushort)item.GetValue("_id").AsInt32;
                //UnityEngine.Debug.Log($"id: {_id}");

                binary.Write(_id);
				binary.Write((byte)item.GetValue("type").AsInt32);
                
                _write_level_field("duration", item, binary);

				var _iteration = 0;
				if (item.TryGetValue("iteration", out BsonValue _value))
				{
					_iteration = _value.AsInt32;
				}
				binary.Write((ushort)_iteration);
				binary.Write((ushort)item.GetValue("delay").AsInt32);
                _write_level_field("chance", item, binary);
				binary.Write(item.GetValue("prefab").AsString);

				var _replicated = false;
				if (item.TryGetValue("replicated", out BsonValue _repl))
				{
					_replicated = _repl.AsBoolean;
				}
				binary.Write(_replicated);
				var _iterationsCount = 0;
				if (item.TryGetValue("iterationsCount", out BsonValue _valueCount))
				{
					_iterationsCount = _valueCount.AsInt32;
				}
				binary.Write((ushort)_iterationsCount);

				_db_autofollow(item, binary);
			}
		}

		private void _skills(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_skills");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				var _id = (ushort)item.GetValue("_id").AsInt32;

				binary.Write(_id);
				binary.Write(_translate("skills", item ,"title"));
				binary.Write(_translate("skills", item, "description"));
				binary.Write((uint)item.GetValue("type").AsInt32);
				binary.Write((ushort)item.GetValue("duration").AsInt32);

                _write_level_field("cooldown", item, binary);

                WriteEmptyOrPresentString("icon", binary, item);
                WriteEmptyOrPresentString("drag_prefab", binary, item);
                WriteEmptyOrPresentString("cast_prefab", binary, item);

				ushort _zero_index = 0;
				if (item.TryGetValue("component", out BsonValue _component))
				{
					if (!_db_autofollow_element(_component.AsBsonDocument, binary))
					{
						binary.Write(_zero_index);
					}
				} else
				{
					binary.Write(_zero_index);
				}

				var _effects_bson = item.GetValue("effects");
				var _effects = new List<ushort>();
				switch (_effects_bson.BsonType)
				{
					case BsonType.Document:
						var _document = _effects_bson.AsBsonDocument;
						for (int k = 0; k < _document.ElementCount; ++k)
						{
							_effects.Add((ushort)_document.GetElement(k).ToBsonDocument().GetValue("Value").AsInt32);
						}
						break;
					default:
						__log.Add(string.Format("BsonType Error:<color=blue>{0}</color>", _effects_bson.BsonType));
						break;
				}
				binary.Write((byte)_effects.Count);
				for (int k = 0; k < _effects.Count; ++k)
				{
					binary.Write(_effects[k]);
				}
			}
			__log.Add(string.Format("<color=blue>_skills</color>: {0}", items.Count));
		}

        private void WriteEmptyOrPresentString(string mongo_key, BinaryWriter binary, BsonDocument item)
        {
            var _string = string.Empty;
            if (item.TryGetValue(mongo_key, out BsonValue value))
            {
                _string = value.AsString;
            }
            binary.Write(_string);
        }

        private void _cards(IMongoDatabase database, BinaryWriter binary)
		{

			var collection = database.GetCollection<BsonDocument>("admin_cards");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var cards = collection.Find(filter).ToList();

			binary.Write((ushort)cards.Count);
			for (int i = 0; i < cards.Count; ++i)
			{
				var card = cards[i];
				binary.Write((ushort)card.GetValue("_id").AsInt32);
				binary.Write(_translate("cards", card, "title"));
				binary.Write(_translate("cards", card, "description"));
				binary.Write(card.GetValue("icon").AsString);
				binary.Write((byte)card.GetValue("type").AsInt32);
				binary.Write((byte)card.GetValue("ManaCost").AsInt32);
				binary.Write((byte)card.GetValue("rarity").AsInt32);
				binary.Write(card.GetValue("enabled").AsBoolean);
				binary.Write(card.GetValue("is_special").AsBoolean);
				binary.Write(card.GetValue("coming_soon").AsBoolean);
				binary.Write((byte)card.GetValue("squadPositionType").AsInt32);

				var _entities_bson = card.GetValue("Entities");
				var _entities = new List<ushort>();
				switch (_entities_bson.BsonType)
				{
					case BsonType.Document:
						var _document = _entities_bson.AsBsonDocument;
						for (int k = 0; k < _document.ElementCount; ++k)
						{
							_entities.Add((ushort)_document.GetElement(k).ToBsonDocument().GetValue("Value").AsInt32);
						}
						break;
					default:
						__log.Add(string.Format("Components Wrong Type:<color=blue>{0}</color>", _entities_bson.BsonType));
						break;
				}
				binary.Write((byte)_entities.Count);
				for(int k = 0; k < _entities.Count; ++k)
				{
					binary.Write(_entities[k]);
				}
			}
			__log.Add(string.Format("<color=blue>_cards</color>: {0}", cards.Count));
		}

		private void _tutorial(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_tutorial");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var tutorials = collection.Find(filter).ToList();

			binary.Write((ushort)tutorials.Count);
			for (int i = 0; i < tutorials.Count; ++i)
			{
				var tutorial = tutorials[i];
				binary.Write((ushort)tutorial.GetValue("_id").AsInt32);
				binary.Write((ushort)tutorial.GetValue("mission").AsInt32);

				var events = tutorial.GetValue("events").AsBsonDocument;
				binary.Write((byte)events.ElementCount);
				for (byte j = 0; j < events.ElementCount; j++)
				{
					var element = events.GetElement(j).Value.AsBsonDocument;
					binary.Write(_bson_generic<byte>("type", element));
					binary.Write(_bson_generic<ushort>("timer", element));
					binary.Write(_bson_generic<byte>("trigger", element));
					binary.Write(_bson_generic<ushort>("param_0", element));
					binary.Write(_bson_generic<float>("param_x", element));
					binary.Write(_bson_generic<float>("param_y", element));
					binary.Write(element.GetValue("message").AsString);
					binary.Write((byte)element.GetValue("analytic_event").AsInt32);
				}
			}
			__log.Add(string.Format("<color=blue>_tutorial</color>: {0}", tutorials.Count));
		}

		private void _menu_tutorial(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_home_tutorial");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var tutorials = collection.Find(filter).ToList();

			binary.Write((ushort)tutorials.Count);
			for (int i = 0; i < tutorials.Count; ++i)
			{
				var tutorial = tutorials[i];
				binary.Write((ushort)tutorial.GetValue("_id").AsInt32);

				var events = tutorial.GetValue("events").AsBsonDocument;
				binary.Write((byte)events.ElementCount);
				for (byte j = 0; j < events.ElementCount; j++)
				{
					var element = events.GetElement(j).Value.AsBsonDocument;
					binary.Write(_bson_generic<byte>("type", element));
					binary.Write(_bson_generic<ushort>("timer", element));
					binary.Write(_bson_generic<byte>("trigger", element));
					binary.Write(element.GetValue("message").AsString);
					binary.Write(element.GetValue("save_state").AsBoolean);
					binary.Write(element.GetValue("skip_on_start").AsBoolean);
					binary.Write((byte)element.GetValue("analytic_event").AsInt32);
				}
			}
			__log.Add(string.Format("<color=blue>_menu_tutorial</color>: {0}", tutorials.Count));
		}

		private void _settings(IMongoDatabase database, BinaryWriter binary)
		{
			var _self = typeof(BinaryDatabaseWriter);
			var _flags = BindingFlags.Instance | BindingFlags.NonPublic;

			var collection = database.GetCollection<BsonDocument>("admin_game_settings");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var settings = collection.Find(filter).ToList();

			ushort _setting_count = 0;
			var _position = binary.BaseStream.Position;
			binary.Write(_setting_count);
			for (int i = 0; i < settings.Count; ++i)
			{
				var _settings = settings[i];
				string _class_name = _settings.GetValue("manager").AsString;
				if (__types.TryGetValue(_class_name, out Type type))
				{
					_setting_count++;
					var _root = _settings.GetValue("settings").AsBsonDocument;
					var _component = (byte[])_self.GetMethod("_address", _flags)
						.MakeGenericMethod(type)
						.Invoke(this, new object[] { _self, _root, _flags });
					binary.Write(_class_name);
					binary.Write((ushort)_component.Length);
					binary.Write(_component);
				}
			}
			binary.BaseStream.Position = _position;
			binary.Write(_setting_count);
			binary.BaseStream.Position = binary.BaseStream.Length;

			__log.Add(string.Format("<color=blue>_settings</color>: {0}", _setting_count));
		}

		private void _heroes(IMongoDatabase database, BinaryWriter binary)
		{

			var collection = database.GetCollection<BsonDocument>("admin_heroes");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				binary.Write((ushort)item.GetValue("_id").AsInt32);
				binary.Write((ushort)item.GetValue("minion").AsInt32);
				binary.Write(_translate("heroes", item, "title"));
				binary.Write(_translate("heroes", item, "description"));
				binary.Write((byte)item.GetValue("type").AsInt32);
				binary.Write(item.GetValue("prefab").AsString);
				binary.Write(item.GetValue("icon").AsString);
				binary.Write(_translate("heroes", item, "second_name"));
				binary.Write(item.GetValue("color").AsString);
                var price = item.GetValue("price").AsBsonDocument;
                binary.Write((uint)price.GetValue("soft").AsInt32);
                binary.Write(price.GetValue("store_key").AsString);
                binary.Write((ushort)price.GetValue("hard").AsInt32);
            }
			__log.Add(string.Format("<color=blue>_heroes</color>: {0}", items.Count));
		}

		private void _battlefields(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_battlefields");
            var filter = new BsonDocument
            {
                {"_deleted", new BsonDocument {{"$exists", false}}},
                {"enabled", true}
            };

            var items = collection.Find(filter).ToList();

            binary.Write((ushort)items.Count);
            for (int i = 0; i < items.Count; ++i)
            {
                var item = items[i];
                binary.Write((ushort)item.GetValue("_id").AsInt32);
				binary.Write((ushort)item.GetValue("rating").AsInt32);
				binary.Write(_translate("battlefields", item, "title"));
				binary.Write(_translate("battlefields", item, "description"));
				binary.Write(item.GetValue("background_color").AsString);
                binary.Write((byte)item.GetValue("type").AsInt32);

                var rating_rules = item.GetValue("rating_rules").AsBsonDocument;
                var victory_rules = rating_rules.GetValue("victory").AsBsonDocument;
                var lose_rules = rating_rules.GetValue("lose").AsBsonDocument;
                bool is_checkpoint = rating_rules.GetValue("is_checkpoint").AsBoolean;
                var victory_rules_soft = victory_rules.GetValue("soft").AsBsonDocument;
                
                binary.Write((byte)victory_rules.GetValue("rating").AsInt32);
                binary.Write((byte)victory_rules.GetValue("accuracy").AsInt32);
                
                binary.Write((ushort)victory_rules_soft.GetValue("amount").AsInt32);
                binary.Write((byte)victory_rules_soft.GetValue("count").AsInt32);
                
                binary.Write((byte)lose_rules.GetValue("rating").AsInt32);
                binary.Write((byte)lose_rules.GetValue("accuracy").AsInt32);
                binary.Write(is_checkpoint);

                var _prefabs = item.GetValue("prefabs").AsBsonDocument;
                binary.Write(_prefabs.GetValue("client").AsString);
                binary.Write(_prefabs.GetValue("server").AsString);

                var _rewards = item.GetValue("rewards").AsBsonDocument;
                binary.Write((byte)_rewards.ElementCount);
                for (int j = 0; j < _rewards.ElementCount; ++j)
                {
                    var reward_item = _rewards.GetElement(j).Value.AsBsonDocument;
                    binary.Write((ushort)reward_item.GetValue("rating").AsInt32);
                    binary.Write((ushort)reward_item.GetValue("reward").AsInt32);                    
                }

				// heroes
                var _heroes = item.GetValue("heroes").AsBsonDocument;
                binary.Write((byte)_heroes.ElementCount);
                for (int j = 0; j < _heroes.ElementCount; ++j)
                {
                    binary.Write((ushort)_heroes.GetElement(j).Value.AsInt32);
                }
                
				// cards
                var _cards = item.GetValue("cards").AsBsonDocument;
                binary.Write((byte)_cards.ElementCount);
                for (int j = 0; j < _cards.ElementCount; ++j)
                {
                    binary.Write((ushort)_cards.GetElement(j).Value.AsInt32);
                }

				// bots
				var _bots = item.GetValue("bots").AsBsonDocument;
				binary.Write((byte)_bots.ElementCount);
				for (int j = 0; j < _bots.ElementCount; ++j)
				{
					binary.Write((ushort)_bots.GetElement(j).Value.AsInt32);
				}

			}
            __log.Add(string.Format("<color=blue>_battlefields</color>: {0}", items.Count));            
		}

		private float _float_int(BsonValue value)
		{
			switch (value.BsonType)
			{
				case BsonType.Double:
					return (float)value.AsDouble;
				case BsonType.Int32:
					return value.AsInt32;
			}
			__log.Add(string.Format("<color=green>_missions effect position type: </color>: {0}", value.BsonType));
			return 0f;
		}

		private void _missions(IMongoDatabase database, BinaryWriter binary)
		{
			var _self = typeof(BinaryDatabaseWriter);
			var _flags = BindingFlags.Instance | BindingFlags.NonPublic;

			var collection = database.GetCollection<BsonDocument>("admin_missions");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				binary.Write((ushort)item.GetValue("_id").AsInt32);
				binary.Write(_translate("missions", item,"title"));
				binary.Write(_translate("missions", item,"description"));
				binary.Write((ushort)item.GetValue("battlefield").AsInt32);
				binary.Write((uint)item.GetValue("win").AsInt32);

				binary.Write((ushort)item.GetValue("player").AsInt32);
				binary.Write((ushort)item.GetValue("enemy").AsInt32);

				var _setting_type = __types["BaseBattleSettings"];
				var _root = item.GetValue("settings").AsBsonDocument;
				var _component = (byte[])_self.GetMethod("_address", _flags)
					.MakeGenericMethod(_setting_type)
					.Invoke(this, new object[] { _self, _root, _flags });
				binary.Write((ushort)_component.Length);
				binary.Write(_component);

                binary.Write((ushort)item.GetValue("reward").AsInt32);

                // static minions
                var _effects = item.GetValue("effects").AsBsonDocument;
				binary.Write((byte)_effects.ElementCount);
				for (int k = 0; k < _effects.ElementCount; ++k)
				{
					var _item = _effects.GetElement(k).Value.AsBsonDocument;
					binary.Write((ushort)_item.GetValue("effect").AsInt32);
					binary.Write((byte)_item.GetValue("side").AsInt32);

					var _position = _item.GetValue("position").AsBsonDocument;
					binary.Write(_float_int(_position.GetValue("x")));
					binary.Write(_float_int(_position.GetValue("z")));

					float _rotation = 0;
					if (_item.TryGetValue("rotation", out BsonValue _value))
					{
						switch (_value.BsonType)
						{
							case BsonType.Double:
								_rotation = (float)_value.AsDouble;
								break;

							case BsonType.Int32:
								_rotation = _value.AsInt32;
								break;
						}
					}
					binary.Write(_rotation);
				}
			}

			__log.Add(string.Format("<color=blue>_missions</color>: {0}", items.Count));
		}

		private void _loots(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_loot");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				binary.Write((ushort)item.GetValue("_id").AsInt32);
				binary.Write(_translate("loot", item, "title"));
				binary.Write(_translate("loot", item, "description"));
				binary.Write(item.GetValue("prefab").AsString);
				binary.Write((byte)item.GetValue("type").AsInt32);

				var currency = item.GetValue("currency").AsBsonDocument;

                binary.Write(_bson_generic<ushort>("shard", currency));

                var hard = currency.GetValue("hard").AsBsonDocument;
                binary.Write(_bson_generic<ushort>("min", hard));
                binary.Write(_bson_generic<ushort>("max", hard));

                var soft = currency.GetValue("soft").AsBsonDocument;
                binary.Write(_bson_generic<ushort>("min", soft));
                binary.Write(_bson_generic<ushort>("max", soft));

                var cards = item.GetValue("cards").AsBsonDocument;
				binary.Write( _bson_generic<ushort>("total", cards) );

				var rest = cards.GetValue("rest").AsBsonDocument;
				binary.Write( _bson_generic<byte>("type", rest) );
                binary.Write( _bson_generic<byte>("variety", rest) );

                binary.Write(_bson_generic<uint>("time", item));

                var options = cards.GetValue("options").AsBsonDocument;
                binary.Write((byte)options.ElementCount);
                for (byte k = 0; k < options.ElementCount; ++k)
                {
                    var element = options.GetElement(k).Value.AsBsonDocument;
                    binary.Write((byte)element.GetValue("type").AsInt32);
                    binary.Write((byte)element.GetValue("variety").AsInt32);
                    binary.Write((byte)element.GetValue("card").AsInt32);
                    binary.Write((ushort)element.GetValue("percent").AsInt32);
                }

            }
			__log.Add(string.Format("<color=blue>_loots</color>: {0}", items.Count));
		}

		private void _company(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_campaigns");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				binary.Write((ushort)item.GetValue("_id").AsInt32);
				binary.Write(_translate("campaigns", item, "title"));
				binary.Write(_translate("campaigns", item, "description"));

				var _open_conditions = item.GetValue("open").AsBsonDocument;
				binary.Write((uint)_open_conditions.GetValue("rating").AsInt32);
				// TODO: date range

				// missions
				var _missions = item.GetValue("missions").AsBsonDocument;
				var _missions_length = (byte)_missions.ElementCount;
				binary.Write(_missions_length);
				for (byte k = 0; k < _missions_length; ++k)
				{
					binary.Write((ushort)_missions.GetElement(k).Value.AsInt32);
				}

				// lives
				var _lives = item.GetValue("attempts").AsBsonDocument;
				var _restore_price = _lives.GetValue("buy").AsBsonDocument;
				binary.Write((ushort)_restore_price.GetValue("count").AsInt32);
				binary.Write((ushort)_restore_price.GetValue("soft").AsInt32);
				binary.Write((ushort)_restore_price.GetValue("hard").AsInt32);
				binary.Write((byte)_lives.GetValue("free").AsInt32);
				binary.Write((uint)_lives.GetValue("restore").AsInt32);

                // reward
                binary.Write((ushort)item.GetValue("reward").AsInt32);                

				// heroes
				var _heroes = item.GetValue("heroes").AsBsonDocument;
				var _heroes_length = (byte)_heroes.ElementCount;
				binary.Write(_heroes_length);
				for (byte k = 0; k < _heroes_length; ++k)
				{
					binary.Write((ushort)_heroes.GetElement(k).Value.AsInt32);
				}
			}

			__log.Add(string.Format("<color=blue>_company</color>: {0}", items.Count));
		}

		// recursion ??
		unsafe private byte[] _address<T>(Type self, BsonDocument root, BindingFlags flags) where T : struct
		{
			var _component = default(T);
			var _component_pointer = (byte*)UnsafeUtility.AddressOf(ref _component);

			var _type = typeof(T);
			var _fields = _type.GetFields();
			var _component_size = UnsafeUtility.SizeOf(_type);
			var _result = new byte[_component_size];

			for (int k = 0; k < _fields.Length; ++k)
			{
				var _info = _fields[k];
				var _offset = UnsafeUtility.GetFieldOffset(_info);
				var _size = UnsafeUtility.SizeOf(_info.FieldType);

				if (root.TryGetValue(_info.Name, out BsonValue value))
				{
					switch (value.BsonType)
					{
						case BsonType.Boolean:
							NetworkUtils.ConvertObject(value.AsBoolean, _info.FieldType, _component_pointer + _offset);
							break;
						case BsonType.Int32:
							NetworkUtils.ConvertObject(value.AsInt32, _info.FieldType, _component_pointer + _offset);
							break;
                        case BsonType.Double:
							NetworkUtils.ConvertObject(value.AsDouble, _info.FieldType, _component_pointer + _offset);
							break;
                        case BsonType.Document:
                            if (_info.FieldType == typeof(BinaryList))
                            {
                                var _binary_list = default(BinaryList);

                                var _bson_document = value.AsBsonDocument;
                                var _length = math.min(_bson_document.ElementCount, BinaryList.elements);
                                for (byte i = 0; i < _length; ++i)
                                {
                                    var _element = _bson_document.GetElement(i).Value;
                                    _binary_list.Add((ushort)_element.AsInt32);
                                }
                                _binary_list.Copy(_component_pointer + _offset, _size);
                            }
                            else
                            {
                                var _bson_document = value.AsBsonDocument;
                                byte[] _value = (byte[])self.GetMethod("_address", flags)
                                    .MakeGenericMethod(_info.FieldType)
                                    .Invoke(this, new object[] { self, _bson_document, flags });
                                fixed (byte* point = &_value[0])
                                {
                                    UnsafeUtility.MemCpy(_component_pointer + _offset, point, _size);
                                }
                            }
							break;

						default:
							__log.Add(string.Format("Field[<color=red>{0}</color>] <color=blue>{1}</color> [<color=green>{2}</color>]", _info.Name, value.BsonType, _type.Name));
							break;
					}
				}
			}

			fixed (byte* point = &_result[0])
			{
				UnsafeUtility.MemCpy(point, _component_pointer, _component_size);
			}

			return _result;
		}


		private void _rewards(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_rewards");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
				binary.Write((ushort)item.GetValue("_id").AsInt32);

                binary.Write(_bson_generic<ushort>("soft", item));
                binary.Write(_bson_generic<ushort>("hard", item));
                binary.Write(_bson_generic<ushort>("shard", item));
                binary.Write(_bson_generic<ushort>("lootbox", item));

				var cards = item.GetValue("cards").AsBsonDocument;
				binary.Write((byte)cards.ElementCount);
				for (byte j = 0; j < cards.ElementCount; j++)
				{
					var element = cards.GetElement(j).Value.AsBsonDocument;
                    binary.Write(_bson_generic<ushort>("card", element));
                    binary.Write(_bson_generic<ushort>("count", element));
                    var rarityElement = element.GetValue("random_rarity").AsBsonDocument;
                    binary.Write(_bson_generic<byte>("rarity", rarityElement));
                    binary.Write(_bson_generic<ushort>("rarity_count", rarityElement));
				}
			}
			__log.Add(string.Format("<color=blue>_rewards</color>: {0}", items.Count));
		}

		private void _bots(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_bots");
			var filter = new BsonDocument("_deleted", new BsonDocument("$exists", 0));
			var items = collection.Find(filter).ToList();

			binary.Write((ushort)items.Count);
			for (int i = 0; i < items.Count; ++i)
			{
				var item = items[i];
                var type = item.GetValue("type").AsInt32;

                binary.Write((ushort)item.GetValue("_id").AsInt32);
				binary.Write(type > 0 ? _translate("bots", item ,"title") : item.GetValue("title").AsString);
				binary.Write((ushort)item.GetValue("hero").AsInt32);
				var _deck = item.GetValue("deck").AsBsonDocument;
				binary.Write((byte)_deck.ElementCount);
				for (byte k = 0; k < _deck.ElementCount; ++k)
				{
					var _info = _deck.GetElement(k).Value.AsBsonDocument;
					byte _level = 1;
					if (_info.TryGetValue("level", out BsonValue value))
					{
						_level = (byte)value.AsInt32;
					}
					binary.Write(_level);
					binary.Write((ushort)_info.GetValue("card").AsInt32);
				}

				binary.Write((bool)(item.GetValue("disabled_at_start").AsBoolean));
				binary.Write((byte)(item.GetValue("disabled_permanently").AsBoolean ? 1 : 0));
				binary.Write((uint)item.GetValue("rating").AsInt32);
				binary.Write((byte)type);
				binary.Write((byte)item.GetValue("hero_lvl").AsInt32);
				binary.Write((byte)item.GetValue("brain_frequency").AsInt32);
				binary.Write((byte)(item.GetValue("canUseTankRange").AsBoolean ? 1 : 0));
			}
			__log.Add(string.Format("<color=blue>_bots</color>: {0}", items.Count));
		}

		private float tryFloatCast(double val)
		{
			if (val > float.MaxValue) {
				throw new ArgumentException($"Wrong float cast. Value: {val}!");
			}
			
			return (float) val;
		}
		private ushort tryUshortCast(int val)
		{
			if (val > ushort.MaxValue) {
				throw new ArgumentException($"Wrong ushort cast. Value: {val}!");
			}

			return (ushort) val;
		}
		private byte tryByteCast(int val)
		{
			if (val > byte.MaxValue) {
				throw new ArgumentException($"Wrong byte cast. Value: {val}!");
			}
			
			return (byte) val;
		}
		
		private void _daylics(IMongoDatabase database, BinaryWriter binary)
		{
			var collection = database.GetCollection<BsonDocument>("admin_daylics");
			var filter = new BsonDocument
			{
				{"_deleted", new BsonDocument {{"$exists", false}}},
				{"enabled", true}
			};

			var daylics = collection.Find(filter).ToList();

			binary.Write(tryUshortCast(daylics.Count));

			foreach (var daylic in daylics)
			{
				var route = daylic["route"].AsBsonDocument;

				binary.Write(tryUshortCast(daylic["_id"].AsInt32));
				binary.Write(tryUshortCast(daylic["treasure"].AsInt32));
				binary.Write(tryUshortCast(daylic["points"].AsInt32));
				binary.Write(tryUshortCast(daylic["need"].AsInt32));
				binary.Write(tryByteCast(daylic["type"].AsInt32));
				binary.Write(tryByteCast(route["ctr"].AsInt32));
				binary.Write(tryByteCast(route["act"].AsInt32));
				binary.Write(_translate("daylics", daylic, "title"));
				binary.Write(_translate("daylics", daylic, "condition"));
				binary.Write(_translate("daylics", daylic, "description"));
				binary.Write(tryUshortCast(daylic["targetCard"].AsInt32));
				binary.Write(tryUshortCast(daylic["targetHero"].AsInt32));
				binary.Write(tryByteCast(daylic["targetRarity"].AsInt32));
			}

			__log.Add($"<color=blue>_daylics</color>: {daylics.Count}");
		}

		private void _shopBank(IMongoDatabase database, BinaryWriter binary)
		{
			var filter = new BsonDocument
			{
				{"_deleted", new BsonDocument {{"$exists", false}}},
				{"enabled", true}
			};
			var items = database
				.GetCollection<BsonDocument>("admin_bank")
				.Find(filter)
				.ToList();

			binary.Write(tryUshortCast(items.Count));
			foreach (var offer in items)
			{
				binary.Write(tryUshortCast(offer["_id"].AsInt32));
				binary.Write(_bson_generic<uint>("count", offer));
				binary.Write(tryUshortCast(offer["hard_price"].AsInt32));
				binary.Write(tryUshortCast(offer["discount"].AsInt32));
				binary.Write(tryByteCast(offer["type"].AsInt32));
				binary.Write(tryByteCast(offer["order"].AsInt32));
				binary.Write(tryFloatCast(offer["price"].ToDouble()));
				binary.Write(_translate("bank", offer, "tilte"));
				binary.Write(_translate("bank", offer, "description"));
				binary.Write(offer["preview"].AsString);
                var store_keys = offer["store_keys"].AsBsonDocument;
                binary.Write(store_keys["android"].AsString);

            }

			__log.Add($"<color=blue>_shop => Bank</color>: {items.Count}");
		}
		
		private void _shopLootBox(IMongoDatabase database, BinaryWriter binary)
		{
			var filter = new BsonDocument
			{
				{"_deleted", new BsonDocument {{"$exists", false}}},
				{"enabled", true}
			};
			var items = database
				.GetCollection<BsonDocument>("admin_market_loots")
				.Find(filter)
				.ToList();

			binary.Write(tryUshortCast(items.Count));
			foreach (var offer in items)
			{
				binary.Write(tryUshortCast(offer["_id"].AsInt32));
				binary.Write(tryUshortCast(offer["lootbox"].AsInt32));
				binary.Write(tryUshortCast(offer["hard_price"].AsInt32));
				binary.Write(tryByteCast(offer["arena"].AsInt32));
				binary.Write(_translate("market_loots", offer, "title"));
				binary.Write(_translate("market_loots", offer, "description"));
				binary.Write(offer["preview"].AsString);
			}

			__log.Add($"<color=blue>_shop => LootBox</color>: {items.Count}");
		}
		
		private void _shopBattlePass(IMongoDatabase database, BinaryWriter binary)
		{
			var filter = new BsonDocument
			{
				{"_deleted", new BsonDocument {{"$exists", false}}},
				{"enabled", true}
			};
			var items = database
				.GetCollection<BsonDocument>("admin_battlepass")
				.Find(filter)
				.ToList();

			binary.Write(tryUshortCast(items.Count));
			foreach (var battlePass in items)
			{
				var time = battlePass["time"].AsBsonDocument;
				var treasure = battlePass["treasure"].AsBsonDocument;

				binary.Write(tryUshortCast(battlePass["_id"].AsInt32));
				binary.Write(tryFloatCast(battlePass["price"].ToDouble()));
				binary.Write(_translate("battlepass", battlePass, "title"));
				binary.Write(time["start"].AsInt32);
				binary.Write(time["duration"].AsInt32);
				
				binary.Write(tryByteCast(treasure.ElementCount));
				foreach (var el in treasure) {
					binary.Write(tryUshortCast(el.Value["free"].AsInt32));
					binary.Write(tryUshortCast(el.Value["pay"].AsInt32));
				}

                var ui_info = battlePass["ui_info"].AsBsonDocument;
                var currencies = ui_info["currencies"].AsBsonDocument;
                var chests = ui_info["chests"].AsBsonDocument;
                binary.Write((uint)currencies["soft"].AsInt32);
                binary.Write((uint)currencies["hard"].AsInt32);
                binary.Write(tryByteCast(chests["common"].AsInt32));
                binary.Write(tryByteCast(chests["rare"].AsInt32));
                binary.Write(tryByteCast(chests["epic"].AsInt32));
                binary.Write(tryByteCast(chests["legendary"].AsInt32));
            }

			__log.Add($"<color=blue>_shop => BattlePass</color>: {items.Count}");
		}
		
		private void _shopAction(IMongoDatabase database, BinaryWriter binary)
		{
			var filter = new BsonDocument
			{
				{"_deleted", new BsonDocument {{"$exists", false}}},
				{"enabled", true}
			};
			var items = database
				.GetCollection<BsonDocument>("admin_actions")
				.Find(filter)
				.ToList();

			binary.Write(tryUshortCast(items.Count));
			foreach (var offer in items)
			{
				binary.Write(tryUshortCast(offer["_id"].AsInt32));
				binary.Write(tryUshortCast(offer["treasure"].AsInt32));
				binary.Write(tryByteCast(offer["buy_limit"].AsInt32));
				binary.Write(tryByteCast(offer["type"].AsInt32));

				var price = offer["price"].AsBsonDocument;
				binary.Write(price["soft"].AsInt32);
				binary.Write(price["hard"].AsInt32);

				var store_keys = offer["store_keys"].AsBsonDocument;
				binary.Write(store_keys["android"].AsString);
				binary.Write(store_keys["ios"].AsString);

				var time = offer["time"].AsBsonDocument;
				binary.Write(time["start"].AsInt32);
				binary.Write(time["duration"].AsInt32);
				binary.Write((byte)time["type"].AsInt32);

				binary.Write(_translate("actions", offer, "title"));
				binary.Write(offer["preview"].AsString);
			}

			__log.Add($"<color=blue>_shop => Action</color>: {items.Count}");
		}

        private void _levels(IMongoDatabase database, BinaryWriter binary)
        {
            var filter = new BsonDocument
            {
                {"_deleted", new BsonDocument {{"$exists", false}}}
            };
            var items = database
                .GetCollection<BsonDocument>("admin_levels")
                .Find(filter)
                .ToList();

            binary.Write(tryUshortCast(items.Count));
            foreach (var level in items)
            {
                binary.Write(tryByteCast(level["level"].AsInt32));
                binary.Write((uint)level["cards_count"].AsInt32);
                binary.Write((uint)level["cards_soft"].AsInt32);
                binary.Write((uint)level["cards_account"].AsInt32);
                binary.Write((uint)level["hero_soft"].AsInt32);
                binary.Write((uint)level["account_exp"].AsInt32);
            }

            __log.Add($"<color=blue>_levels</color>: {items.Count}");
        }

        private void _grid(BinaryWriter binary)
        {
            var _tile_size = ObserverSettings.TileSize.FloatValue;
            var _grid_width = ObserverSettings.GridWidth.FloatValue;
            var _grid_height = ObserverSettings.GridHeight.FloatValue;
            binary.Write(_tile_size);
            binary.Write(_grid_width);
            binary.Write(_grid_height);

            var _instance = UnityEngine.GameObject.Find("BattleInstance");
            if (_instance != null)
            {    
                var _tiles = _instance.GetComponentsInChildren<Observer.TileBehaviour>();
                binary.Write((ushort)_tiles.Length);
                for (int i = 0; i < _tiles.Length; ++i)
                {
                    var _tile = _tiles[i];
                    binary.Write((byte)_tile.Status);
                    binary.Write(_tile.Layer);
                    binary.Write(_tile.transform.position.x);
                    binary.Write(_tile.transform.position.z);
                }
                __log.Add(string.Format("<color=blue>_grid</color>: {0}", _tiles.Length));
            } else
            {
                __log.Add("<color=red>_grid</color>: instance error");
            }
        }


        public void Write(string mongo, WriteType writeType)
		{
			__log.Add(string.Format("{0}", "Write Statistic:"));

			// mongo magic
			var client = new MongoClient(mongo);
			var database = client.GetDatabase("game_legacy");
			
			fillTranslate(database);

			try
			{
				ConfigVar.Load();
				if ((writeType & WriteType.Base) > 0)
				{
					var _file_stream = new FileStream(__database, FileMode.Create);
					using (var _binary = new BinaryWriter(_file_stream))
					{
						_binary.Write(UnityEngine.Application.version);
						_cards(database, _binary);//title,description
						_linker(database, _binary);//-
						_minions(database, _binary);//-
						_effects(database, _binary);//-
						_skills(database, _binary);//title,description
						_heroes(database, _binary);//title,description,second_name
						_missions(database, _binary);//title,description
						_tutorial(database, _binary);//-
						_menu_tutorial(database, _binary);//-
						_company(database, _binary);//title,description
						_battlefields(database, _binary);//title,description
						_loots(database, _binary);//title,description
						_rewards(database, _binary);//-
						_bots(database, _binary);//title
						_settings(database, _binary);//-
						_daylics(database, _binary);//title,condition,description
						_shopBank(database, _binary);//tilte,description
						_shopLootBox(database, _binary);//title,description
						_shopBattlePass(database, _binary);//title
						_shopAction(database, _binary);//title
                        _levels(database, _binary);
					}
				}

				if ((writeType & WriteType.Grid) > 0)
				{
					var _grid_file_stream = new FileStream(__databaseGrid, FileMode.Create);
					using (var _binary_grid = new BinaryWriter(_grid_file_stream))
					{
						_binary_grid.Write(Components.Instance.Hash);
						_grid(_binary_grid);
					}
				}
			}
			catch (Exception error)
			{
				__log.Add(string.Format("{0}: {1}", "Exception", error.StackTrace));
				__log.Add(error.StackTrace);
			}

			UnityEngine.Debug.Log(string.Join("\n", __log));

			BinaryDatabase.Instance.Dispose();
		}
    }
}
 