using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using AslHelp.GameEngines.Unity.Memory;
using AslHelp.Shared;
using AslHelp.Shared.Extensions;

namespace AslHelp.GameEngines.Unity.Collections;

internal sealed partial class NetFxDictionary(
    IUnityReader memory,
    int[] buckets,
    NetFxDictionary.Entry[] entries,
    int count) : IReadOnlyDictionary<string, string?>
{
    private readonly int[] _buckets = buckets;
    private readonly Entry[] _entries = entries;

    private readonly IUnityReader _memory = memory;
    private readonly string?[] _keyCache = new string?[entries.Length];
    private readonly string?[] _valueCache = new string?[entries.Length];

    public int Count { get; } = count;

    public IEnumerable<string> Keys
    {
        get
        {
            Enumerator enumerator = new(this);
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current.Key;
            }
        }
    }

    public IEnumerable<string?> Values
    {
        get
        {
            Enumerator enumerator = new(this);
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current.Value;
            }
        }
    }

    public unsafe string? this[string key]
    {
        get
        {
            ref string? value = ref FindValue(key);
            if (Unsafe.AsPointer(ref value) != null)
            {
                return value;
            }

            string msg = $"The given key '{key}' was not present in the dictionary.";
            ThrowHelper.ThrowKeyNotFoundException(msg);

            return default;
        }
    }

    public unsafe bool ContainsKey(string key)
    {
        return Unsafe.AsPointer(ref FindValue(key)) != null;
    }

    public unsafe bool TryGetValue(string key, [MaybeNullWhen(false)] out string? value)
    {
        ref string? valRef = ref FindValue(key);
        if (Unsafe.AsPointer(ref valRef) != null)
        {
            value = valRef;
            return true;
        }

        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<string, string?>> GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private ref int GetBucket(uint hashCode)
    {
        return ref _buckets[(hashCode & int.MaxValue) % _buckets.Length];
    }

    private unsafe ref string? FindValue(string key)
    {
        ref string? value = ref Unsafe.AsRef<string?>(null);

        Entry[] entries = _entries;

        uint hashCode = (uint)key.GetHashCode();
        int i = GetBucket(hashCode) - 1;

        while (i >= 0)
        {
            if ((uint)i >= (uint)entries.Length)
            {
                break;
            }

            ref Entry entry = ref entries[i];
            if (entry.HashCode == hashCode
                && GetKey(i, entry) == key)
            {
                value = ref GetValue(i, entry);
                break;
            }

            i = entry.Next;
        }

        return ref value;
    }

    /// <remarks>
    ///     Same implementation as <see cref="UnityMemory.ReadString(nint, int[])"/>.<br/>
    ///     Can't call that method since it expects the address of the pointer to the string.<br/>
    ///     Might need to introduce methods which accept the raw starting address.
    /// </remarks>
    private string GetKey(int index, Entry entry)
    {
        if (_keyCache[index] is string key)
        {
            return key;
        }

        nint deref = entry.Key;
        int length = _memory.Read<int>(deref + (_memory.PointerSize * 2));

        char[]? rented = null;
        Span<char> buffer = length <= 512
            ? stackalloc char[512]
            : (rented = ArrayPool<char>.Shared.Rent(length));

        _memory.ReadArray(buffer[..length], deref + (_memory.PointerSize * 2) + sizeof(int));
        string result = buffer[..length].ToString();

        ArrayPool<char>.Shared.ReturnIfNotNull(rented);
        _keyCache[index] = result;

        return result;
    }

    /// <remarks>
    ///     Same implementation as <see cref="UnityMemory.ReadString(nint, int[])"/>.<br/>
    ///     Can't call that method since it expects the address of the pointer to the string.<br/>
    ///     Might need to introduce methods which accept the raw starting address.
    /// </remarks>
    private ref string? GetValue(int index, Entry entry)
    {
        ref string? value = ref _valueCache[index];
        if (value is not null)
        {
            return ref value;
        }

        nint deref = entry.Value;
        if (deref == 0)
        {
            return ref value;
        }

        int length = _memory.Read<int>(deref + (_memory.PointerSize * 2));

        char[]? rented = null;
        Span<char> buffer = length <= 512
            ? stackalloc char[512]
            : (rented = ArrayPool<char>.Shared.Rent(length));

        _memory.ReadArray(buffer[..length], deref + (_memory.PointerSize * 2) + sizeof(int));
        value = buffer[..length].ToString();

        ArrayPool<char>.Shared.ReturnIfNotNull(rented);
        _keyCache[index] = value;

        return ref value;
    }
}
