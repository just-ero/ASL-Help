using System.Diagnostics.CodeAnalysis;

using AslHelp.Memory;
using AslHelp.Memory.Native;
using AslHelp.Shared;

public partial class Basic
{
    public void Write<T>(T value, int baseOffset, params int[] offsets)
        where T : unmanaged
    {
        Write(value, MainModule, baseOffset, offsets);
    }

    public void Write<T>(T value, string moduleName, int baseOffset, params int[] offsets)
        where T : unmanaged
    {
        Write(value, Modules[moduleName], baseOffset, offsets);
    }

    public void Write<T>(T value, Module module, int baseOffset, params int[] offsets)
        where T : unmanaged
    {
        Write(value, module.Base + baseOffset, offsets);
    }

    public unsafe void Write<T>(T value, nint baseAddress, params int[] offsets)
        where T : unmanaged
    {
        nint deref = Deref(baseAddress, offsets);
        int size = GetNativeSizeOf<T>();

        if (!WinInteropWrapper.WriteMemory(_handle, deref, &value, size))
        {
            string msg = $"Failed to write memory at {(ulong)deref:X}: {WinInteropWrapper.GetLastWin32ErrorMessage()}";
            ThrowHelper.ThrowException(msg);
        }
    }

    public bool TryWrite<T>(T value, int baseOffset, params int[] offsets)
        where T : unmanaged
    {
        return TryWrite(value, MainModule, baseOffset, offsets);
    }

    public bool TryWrite<T>(T value, [NotNullWhen(true)] string? moduleName, int baseOffset, params int[] offsets)
        where T : unmanaged
    {
        if (moduleName is null)
        {
            return false;
        }

        return TryWrite(value, Modules[moduleName], baseOffset, offsets);
    }

    public bool TryWrite<T>(T value, [NotNullWhen(true)] Module? module, int baseOffset, params int[] offsets)
        where T : unmanaged
    {
        if (module is null)
        {
            return false;
        }

        return TryWrite(value, module.Base + baseOffset, offsets);
    }

    public unsafe bool TryWrite<T>(T value, nint baseAddress, params int[] offsets)
        where T : unmanaged
    {
        if (!TryDeref(out nint deref, baseAddress, offsets))
        {
            return false;
        }

        int size = GetNativeSizeOf<T>();

        return WinInteropWrapper.WriteMemory(_handle, deref, &value, size);
    }
}
