using System.Collections;
using System.Collections.Generic;

namespace AslHelp.GameEngines.Unity.Collections;

internal partial class NetFxDictionary
{
    public struct Entry
    {
        public int HashCode;
        public int Next;
        public nint Key;
        public nint Value;
    }

    private struct Enumerator : IEnumerator<KeyValuePair<string, string?>>, IEnumerator
    {
        private readonly NetFxDictionary _dictionary;

        private int _next;

        public Enumerator(NetFxDictionary dictionary)
        {
            _dictionary = dictionary;
        }

        public KeyValuePair<string, string?> Current { get; private set; }
        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            uint next = (uint)_next, count = (uint)_dictionary.Count;

            // Use unsigned comparison, since we set `index` to `_dictionary.Count + 1` when the enumeration ends.
            // `_dictionary.Count + 1` could be negative if `_dictionary.Count` is `int.MaxValue`.
            while (next < count)
            {
                ref Entry entry = ref _dictionary._entries[next];
                if (entry.Next >= -1)
                {
                    Current = new(_dictionary.GetKey((int)next, entry), _dictionary.GetValue((int)next, entry));
                    _next = (int)next + 1;

                    return true;
                }

                next++;
            }

            _next = (int)count + 1;
            Current = default;

            return false;
        }

        public void Reset()
        {
            _next = 0;
            Current = default;
        }

        public readonly void Dispose() { }
    }
}