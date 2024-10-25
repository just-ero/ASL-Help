using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using AslHelp.GameEngines.Unity.Memory;
using AslHelp.Shared;
using AslHelp.Shared.Extensions;

namespace AslHelp.GameEngines.Unity.Collections;

internal sealed partial class NetFxHashSet(
    IUnityReader memory,
    int[] buckets,
    NetFxHashSet<nint>.Slot[] slots,
    int count,
    int lastIndex) : ISet<string?>, IReadOnlyCollection<string?>
{
    private const int Lower31BitMask = 0x7FFFFFFF;

    private readonly int[] _buckets = buckets;
    private readonly NetFxHashSet<nint>.Slot[] _slots = slots;

    private readonly int _lastIndex = lastIndex;

    private readonly IUnityReader _memory = memory;
    private readonly string?[] _slotCache = new string?[slots.Length];

    public int Count { get; } = count;
    public bool IsReadOnly { get; } = true;

    public bool Contains(string? item)
    {
        return InternalIndexOf(item) >= 0;
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

        int numCopied = 0;
        for (int i = 0; i < _lastIndex && numCopied < Count; i++)
        {
            NetFxHashSet<nint>.Slot slot = _slots[i];
            if (slot.HashCode >= 0)
            {
                array[arrayIndex + numCopied] = GetSlotValue(i, slot);
                numCopied++;
            }
        }
    }

    public bool IsSubsetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (Count == 0)
        {
            return true;
        }

        if (other is HashSet<string?> otherSet && otherSet.Comparer == EqualityComparer<string?>.Default)
        {
            if (Count > otherSet.Count)
            {
                return false;
            }

            return IsSubsetOfHasSetWithSameEC(otherSet);
        }

        ElementCount result = CheckUniqueAndUnfoundElements(other, false);
        return result.UniqueCount == Count && result.UnfoundCount >= 0;
    }

    private bool IsSubsetOfHasSetWithSameEC(HashSet<string?> other)
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

    public bool IsProperSubsetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (other is ICollection<string?> { Count: > 0 })
        {
            return Count == 0;
        }

        if (other is HashSet<string?> otherSet && otherSet.Comparer == EqualityComparer<string?>.Default)
        {
            if (Count >= otherSet.Count)
            {
                return false;
            }

            return IsSubsetOfHasSetWithSameEC(otherSet);
        }

        ElementCount result = CheckUniqueAndUnfoundElements(other, false);
        return result.UniqueCount == Count && result.UnfoundCount > 0;
    }

    public bool IsSupersetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (other is ICollection<string?> { Count: 0 })
        {
            return true;
        }

        if (other is HashSet<string?> otherSet && otherSet.Comparer == EqualityComparer<string?>.Default)
        {
            if (Count < otherSet.Count)
            {
                return false;
            }
        }

        return ContainsAllElements(other);
    }

    public bool IsProperSupersetOf(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (Count == 0)
        {
            return false;
        }

        if (other is ICollection<string?> { Count: 0 })
        {
            return true;
        }

        if (other is HashSet<string?> otherSet && otherSet.Comparer == EqualityComparer<string?>.Default)
        {
            if (Count <= otherSet.Count)
            {
                return false;
            }

            return ContainsAllElements(otherSet);
        }

        ElementCount result = CheckUniqueAndUnfoundElements(other, false);
        return result.UniqueCount < Count && result.UnfoundCount == 0;
    }

    public bool Overlaps(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (Count == 0)
        {
            return false;
        }

        foreach (string? element in other)
        {
            if (Contains(element))
            {
                return true;
            }
        }

        return false;
    }

    public bool SetEquals(IEnumerable<string?> other)
    {
        ThrowHelper.ThrowIfNull(other);

        if (other is HashSet<string?> otherSet && otherSet.Comparer == EqualityComparer<string?>.Default)
        {
            if (Count != otherSet.Count)
            {
                return false;
            }

            return ContainsAllElements(otherSet);
        }

        if (other is ICollection<string?> otherCollection && Count == 0 && otherCollection.Count > 0)
        {
            return false;
        }

        ElementCount result = CheckUniqueAndUnfoundElements(other, true);
        return result.UniqueCount == Count && result.UnfoundCount == 0;
    }

    private unsafe ElementCount CheckUniqueAndUnfoundElements(IEnumerable<string?> other, bool returnIfUnfound)
    {
        ElementCount result = default;

        if (Count == 0)
        {
            result.UniqueCount = 0;
            result.UnfoundCount = other.Any() ? 1 : 0;

            return result;
        }

        foreach (string? item in other)
        {
            int index = InternalIndexOf(item);
            if (index >= 0)
            {
                result.UniqueCount++;
            }
            else
            {
                result.UnfoundCount++;
                if (returnIfUnfound)
                {
                    break;
                }
            }
        }

        return result;
    }

    private int InternalIndexOf(string? item)
    {
        int hashCode = InternalGetHashCode(item);

        int i = _buckets[hashCode % _buckets.Length] - 1;
        while (i >= 0)
        {
            NetFxHashSet<nint>.Slot slot = _slots[i];
            if (slot.HashCode == hashCode && GetSlotValue(i, slot) == item)
            {
                return i;
            }

            i = slot.Next;
        }

        return -1;
    }

    private int InternalGetHashCode(string? item)
    {
        return EqualityComparer<string?>.Default.GetHashCode(item) & Lower31BitMask;
    }

    private bool ContainsAllElements(IEnumerable<string?> other)
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

    public IEnumerator<string?> GetEnumerator()
    {
        return new Enumerator(this);
    }

    bool ISet<string?>.Add(string? item)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);

        return false;
    }

    bool ICollection<string?>.Remove(string? item)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);

        return false;
    }

    void ICollection<string?>.Clear()
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    void ISet<string?>.ExceptWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    void ISet<string?>.IntersectWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    void ISet<string?>.SymmetricExceptWith(IEnumerable<string?> other)
    {
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    void ISet<string?>.UnionWith(IEnumerable<string?> other)
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
        const string Msg = "The collection is read-only.";
        ThrowHelper.ThrowNotSupportedException(Msg);
    }

    /// <remarks>
    ///     Same implementation as <see cref="UnityMemory.ReadString(nint, int[])"/>.<br/>
    ///     Can't call that method since it expects the address of the pointer to the string.<br/>
    ///     Might need to introduce methods which accept the raw starting address.
    /// </remarks>
    private string? GetSlotValue(int index, NetFxHashSet<nint>.Slot slot)
    {
        if (_slotCache[index] is string value)
        {
            return value;
        }

        nint deref = slot.Value;
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
