using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Legacy.Database;
using MongoDB.Bson;
using MongoDB.Driver;
using UnityEngine;

namespace Legacy.Observer
{
    public class DBUpdater<TDocument> : DBUpdate
    {
        private readonly List<Dictionary<string, UpdateDefinition<TDocument>>> _allUpdates;
        private readonly UpdateDefinitionBuilder<TDocument> _update;

        /** for update **/
        private readonly IMongoCollection<TDocument> _collection;
        private readonly BsonDocument _filter;
        private readonly FindOneAndUpdateOptions<TDocument> _options;

        public DBUpdater(IMongoCollection<TDocument> collection, uint uid)
        {
            _allUpdates = new List<Dictionary<string, UpdateDefinition<TDocument>>>();
            _allUpdates.Add(new Dictionary<string, UpdateDefinition<TDocument>>());

            _update = Builders<TDocument>.Update;

            _collection = collection;
            _filter = new BsonDocument {{"_id", uid}};
            _options = new FindOneAndUpdateOptions<TDocument> {ReturnDocument = ReturnDocument.After};
        }

        private void AddOrSetUpdate(string key, UpdateDefinition<TDocument> value, int index = 0)
        {
            // 
            bool high = false;
            bool low = false;
            foreach (var val in _allUpdates[index])
            {
                if (val.Key.StartsWith(key))
                {
                    high = true;
                    break;
                }

                if (key.StartsWith(val.Key))
                {
                    low = true;
                    break;
                }
            }

            if (high)
            {
                foreach (var updates in _allUpdates)
                {
                    var itemsToRemove = updates
                        .Where(x => x.Key.StartsWith(key)).ToArray();
                    foreach (var item in itemsToRemove)
                    {
                        updates.Remove(item.Key);
                    }
                }

                _allUpdates[index].Add(key, value);
            }
            else if (low)
            {
                if (_allUpdates.ElementAtOrDefault(index + 1) == null)
                {
                    _allUpdates.Add(new Dictionary<string, UpdateDefinition<TDocument>>());
                }

                AddOrSetUpdate(key, value, index + 1);
            }
            else
            {
                _allUpdates[index].Add(key, value);
            }
        }

        public void Set<TField>(string field, TField value)
        {
            AddOrSetUpdate(field, _update.Set(field, value));
        }

        public void Inc<TField>(string field, TField value)
        {
            AddOrSetUpdate(field, _update.Inc(field, value));
        }

        public void Unset(string field)
        {
            AddOrSetUpdate(field, _update.Unset(field));
        }

        public void Update()
        {
            foreach (var updates in _allUpdates)
            {
                if (updates.Count > 0)
                {
                    var t = typeof(TDocument);
                    Debug.Log($"-------------------------->{t}: {updates.Count}<------------------------");
                    foreach (var el in updates)
                    {
                        Debug.Log($"-------------------------->{el.Key}<------------------------");
                    }

                    _collection.FindOneAndUpdate(_filter, _update.Combine(updates.Values.ToList()), _options);
                    updates.Clear();
                }
            }
        }

        public void Push<TField>(string field, TField value)
        {
            AddOrSetUpdate(field, _update.Push(field, value));
        }
        
        public void Pull<TField>(string field, TField value)
        {
            AddOrSetUpdate(field, _update.Pull(field, value));
        }

        public void AddToSet<TField>(string field, TField value)
        {
            AddOrSetUpdate(field, _update.AddToSet(field, value));
        }
    }
}