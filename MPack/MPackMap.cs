using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS
{
    public class MPackMap : MPack, IDictionary<string, MPack>
    {
        public int Count { get { return _collection.Count; } }
        public bool IsReadOnly { get { return _collection.IsReadOnly; } }
        public ICollection<string> Keys { get { return _collection.Keys; } }
        public ICollection<MPack> Values { get { return _collection.Values; } }
        public override object Value { get { return _collection; } }
        public override MPackType ValueType { get { return MPackType.Map; } }

        private IDictionary<string, MPack> _collection;

        public MPackMap()
        {
            _collection = new Dictionary<string, MPack>(StringComparer.InvariantCultureIgnoreCase);
        }
        public MPackMap(IDictionary<string, MPack> seed)
        {
            _collection = new Dictionary<string, MPack>(seed, StringComparer.InvariantCultureIgnoreCase);
        }
        public MPackMap(IEnumerable<KeyValuePair<string, MPack>> seed)
        {
            _collection = new Dictionary<string, MPack>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var v in seed)
                _collection.Add(v);
        }

        public override MPack this[string key]
        {
            get { return _collection[key]; }
            set { _collection[key] = value; }
        }

        public IEnumerator<KeyValuePair<string, MPack>> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(KeyValuePair<string, MPack> item)
        {
            _collection.Add(item);
        }
        public void Clear()
        {
            _collection.Clear();
        }
        public bool Contains(KeyValuePair<string, MPack> item)
        {
            return _collection.Contains(item);
        }
        public void CopyTo(KeyValuePair<string, MPack>[] array, int arrayIndex)
        {
            _collection.CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<string, MPack> item)
        {
            return _collection.Remove(item);
        }
        public bool ContainsKey(string key)
        {
            return _collection.ContainsKey(key);
        }
        public bool ContainsKeys(IEnumerable<string> keys)
        {
            return keys.All(ContainsKey);
        }
        public void Add(string key, MPack value)
        {
            _collection.Add(key, value);
        }
        public bool Remove(string key)
        {
            return _collection.Remove(key);
        }
        public bool TryGetValue(string key, out MPack value)
        {
            return _collection.TryGetValue(key, out value);
        }

        public override string ToString()
        {
            return String.Join(",", this.Select(kvp => kvp.Key + ":" + kvp.Value.ToString()));
        }
    }

}
