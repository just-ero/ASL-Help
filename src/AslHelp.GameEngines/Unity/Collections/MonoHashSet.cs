using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;

using AslHelp.GameEngines.Unity.Memory;
using AslHelp.Shared;
using AslHelp.Shared.Extensions;

namespace AslHelp.GameEngines.Unity.Collections;

internal sealed partial class MonoHashSet(
    IUnityReader memory,
    int[] table,
    Link[] links,
    nint[] slots,
    int touched,
    int count) : ISet<string?>, IReadOnlyCollection<string?>
{
    private const int NoSlot = -1;
    private const int HashFlag = int.MinValue;

    private readonly int[] _table = table;
    private readonly Link[] _links = links;

    private readonly nint[] _slots = slots;

    private readonly int _touched = touched;

    private readonly IUnityReader _memory = memory;
    private readonly string?[] _slotCache = new string?[slots.Length];

    public int Count { get; } = count;
    public bool IsReadOnly { get; } = true;

    public bool Contains(string? item)
    {
        return FindItem(item);
    }

    public void CopyTo(string?[] array)
    {
        CopyTo(array, 0, Count);
    }

    public void CopyTo(string?[] array, int arrayIndex)
    {
        CopyTo(array, arrayIndex, Count);
    }

    public void CopyTo(string?[] array, int arrayIndex, int count)
    {
        ThrowHelper.ThrowIfNotInRange(arrayIndex, 0, array.Length);
        ThrowHelper.ThrowIfNotInRange(count, 0, Count);

        for (int i = 0; i < _touched; i++)
        {
            if (GetLinkHashCode(i) != 0)
            {
                array[arrayIndex++] = GetSlotValue(i);
            }
        }
    }

    private int GetLinkHashCode(int index)
    {
        return _links[index].HashCode & HashFlag;
    }

    public bool IsSubsetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (Count == 0)
        {
            return true;
        }

        HashSet<string?> otherSet = ToSet(other);
        if (Count > otherSet.Count)
        {
            return false;
        }

        return CheckIsSubsetOf(otherSet);
    }

    public bool IsProperSubsetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (Count == 0)
        {
            return true;
        }

        HashSet<string?> otherSet = ToSet(other);
        if (Count >= otherSet.Count)
        {
            return false;
        }

        return CheckIsSubsetOf(otherSet);
    }

    public bool IsSupersetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        HashSet<string?> otherSet = ToSet(other);
        if (Count < otherSet.Count)
        {
            return false;
        }

        return CheckIsSupersetOf(otherSet);
    }

    public bool IsProperSupersetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        HashSet<string?> otherSet = ToSet(other);
        if (Count <= otherSet.Count)
        {
            return false;
        }

        return CheckIsSupersetOf(otherSet);
    }

    public bool Overlaps(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        foreach (string? item in other)
        {
            if (Contains(item))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetEquals(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        HashSet<string?> otherSet = ToSet(other);
        if (Count != otherSet.Count)
        {
            return false;
        }

        return CheckIsSupersetOf(otherSet);
    }

    public IEnumerator<string?> GetEnumerator()
    {
        return new Enumerator(this);
    }

    public bool Add(string? item)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);

        return false;
    }

    public bool Remove(string? item)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);

        return false;
    }

    public void Clear()
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    public void ExceptWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    public void IntersectWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    public void SymmetricExceptWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    public void UnionWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    void ICollection<string?>.Add(string? item)
    {
        Add(item);
    }

    private HashSet<string?> ToSet(IEnumerable<string?> source)
    {
        if (source is not HashSet<string?> set
            || set.Comparer != EqualityComparer<string?>.Default)
        {
            set = new(source, EqualityComparer<string?>.Default);
        }

        return set;
    }

    private bool CheckIsSubsetOf(HashSet<string?> other)
    {
        foreach (string? item in this)
        {
            if (!other.Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    private bool CheckIsSupersetOf(HashSet<string?> other)
    {
        foreach (string? item in other)
        {
            if (!Contains(item))
            {
                return false;
            }
        }

        return true;
    }

    private int GetItemHashCode(string? item)
    {
        if (item is null)
        {
            return 0;
        }

        return item.GetHashCode() | HashFlag;
    }

    private ref int GetSlot(int hashCode)
    {
        return ref _table[(hashCode & int.MaxValue) % _table.Length];
    }

    private bool FindItem(string? item)
    {
        int hashCode = GetItemHashCode(item);
        int i = GetSlot(hashCode) - 1;

        while (i != NoSlot)
        {
            Link link = _links[i];
            if (link.HashCode == hashCode
                && GetSlotValue(i) == item)
            {
                return true;
            }

            i = link.Next;
        }

        return false;
    }

    private string? GetSlotValue(int index)
    {
        if (_slotCache[index] is string value)
        {
            return value;
        }

        nint deref = _slots[index];
        if (deref == 0)
        {
            return null;
        }

        int length = _memory.Read<int>(deref + (_memory.PointerSize * 2));

        char[]? rented = null;
        Span<char> buffer = length <= 512
            ? stackalloc char[512]
            : (rented = ArrayPool<char>.Shared.Rent(length));

        _memory.ReadArray(buffer[..length], deref + (_memory.PointerSize * 2) + sizeof(int));
        value = buffer[..length].ToString();

        ArrayPool<char>.Shared.ReturnIfNotNull(rented);
        _slotCache[index] = value;

        return value;
    }
}
