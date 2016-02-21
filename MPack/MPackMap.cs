using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS
{
    public class MPackMap : MPack, IDictionary<MPack, MPack>
    {
        public int Count { get { return _collection.Count; } }
        public bool IsReadOnly { get { return _collection.IsReadOnly; } }
        public ICollection<MPack> Keys { get { return _collection.Keys; } }
        public ICollection<MPack> Values { get { return _collection.Values; } }
        public override object Value { get { return _collection; } }
        public override MPackType ValueType { get { return MPackType.Map; } }

        private IDictionary<MPack, MPack> _collection;

        public MPackMap()
        {
            _collection = new Dictionary<MPack, MPack>();
        }
        public MPackMap(IDictionary<MPack, MPack> seed)
        {
            _collection = new Dictionary<MPack, MPack>(seed);
        }
        public MPackMap(IEnumerable<KeyValuePair<MPack, MPack>> seed)
        {
            _collection = new Dictionary<MPack, MPack>();
            foreach (var v in seed)
                _collection.Add(v);
        }

        public override MPack this[MPack key]
        {
            get { return _collection[key]; }
            set { _collection[key] = value; }
        }

        public IEnumerator<KeyValuePair<MPack, MPack>> GetEnumerator()
        {
            return _collection.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(KeyValuePair<MPack, MPack> item)
        {
            _collection.Add(item);
        }
        public void Clear()
        {
            _collection.Clear();
        }
        public bool Contains(KeyValuePair<MPack, MPack> item)
        {
            return _collection.Contains(item);
        }
        public void CopyTo(KeyValuePair<MPack, MPack>[] array, int arrayIndex)
        {
            _collection.CopyTo(array, arrayIndex);
        }
        public bool Remove(KeyValuePair<MPack, MPack> item)
        {
            return _collection.Remove(item);
        }
        public bool ContainsKey(MPack key)
        {
            return _collection.ContainsKey(key);
        }
        public bool ContainsKeys(IEnumerable<MPack> keys)
        {
            return keys.All(ContainsKey);
        }
        public void Add(MPack key, MPack value)
        {
            _collection.Add(key, value);
        }
        public bool Remove(MPack key)
        {
            return _collection.Remove(key);
        }
        public bool TryGetValue(MPack key, out MPack value)
        {
            return _collection.TryGetValue(key, out value);
        }

        public override string ToString()
        {
            return String.Join(",", this.Select(kvp => kvp.Key.ToString() + ":" + kvp.Value.ToString()));
        }
    }

}
