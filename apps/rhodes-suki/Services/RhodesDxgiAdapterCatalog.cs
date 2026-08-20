using System.Runtime.InteropServices;

namespace RhodesSuki.Services;

public sealed record RhodesDxgiAdapterDescriptor(
    int DeviceId,
    string Name,
    ulong DedicatedVideoMemoryBytes,
    ulong SharedSystemMemoryBytes,
    bool IsSoftware,
    bool? IsIntegrated = null);

public sealed record SukiMaaInferenceDeviceOption(
    int DeviceId,
    string Name,
    ulong DedicatedVideoMemoryBytes,
    ulong SharedSystemMemoryBytes,
    bool IsSoftware,
    bool? IsIntegrated,
    bool IsRecommended,
    bool IsFallback = false)
{
    public bool IsIntegratedAdapter => !IsSoftware
        && (IsIntegrated ?? DedicatedVideoMemoryBytes == 0);

    public bool IsDiscrete => !IsSoftware && !IsIntegratedAdapter;

    public string AdapterKindLabel => IsSoftware
        ? "ソフトウェアGPU"
        : IsDiscrete ? "専用GPU" : "内蔵GPU";

    public string MemoryLabel => IsDiscrete
        ? FormatMemory(DedicatedVideoMemoryBytes)
        : SharedSystemMemoryBytes > 0 ? $"共有 {FormatMemory(SharedSystemMemoryBytes)}" : "容量不明";

    public string DisplayName => IsFallback
        ? $"GPU {DeviceId} · 保存済みのGPU（再検出できません）"
        : $"GPU {DeviceId} · {Name} · {AdapterKindLabel} · {MemoryLabel}{(IsRecommended ? "（推奨）" : "")}";

    public string Detail => IsFallback
        ? $"以前保存されたDirectML device ID {DeviceId}を保持しています。GPUを再検出できるまで番号は変更しません。"
        : $"DirectML device ID {DeviceId}。DXGIの列挙順をそのまま使用します。{AdapterKindLabel} / {MemoryLabel}";

    private static string FormatMemory(ulong bytes)
    {
        if (bytes == 0)
            return "容量不明";

        var gibibytes = bytes / (1024d * 1024d * 1024d);
        return gibibytes >= 10 || Math.Abs(gibibytes - Math.Round(gibibytes)) < 0.05
            ? $"{gibibytes:0} GB"
            : $"{gibibytes:0.0} GB";
    }
}

public static class RhodesDxgiAdapterCatalog
{
    private const int DxgiErrorNotFound = unchecked((int)0x887A0002);
    private const uint DxgiAdapterFlagSoftware = 2;

    public static IReadOnlyList<SukiMaaInferenceDeviceOption> Discover(int preferredDeviceId)
    {
        try
        {
            return BuildOptions(Enumerate(), preferredDeviceId);
        }
        catch (Exception ex) when (ex is COMException
                                   or DllNotFoundException
                                   or EntryPointNotFoundException
                                   or PlatformNotSupportedException
                                   or InvalidCastException)
        {
            return BuildOptions([], preferredDeviceId);
        }
    }

    public static IReadOnlyList<SukiMaaInferenceDeviceOption> BuildOptions(
        IReadOnlyList<RhodesDxgiAdapterDescriptor> descriptors,
        int preferredDeviceId)
    {
        preferredDeviceId = Math.Clamp(preferredDeviceId, 0, 15);
        var physical = descriptors
            .Where(descriptor => !descriptor.IsSoftware)
            .OrderBy(descriptor => descriptor.DeviceId)
            .ToArray();
        if (physical.Length == 0)
        {
            return
            [
                new SukiMaaInferenceDeviceOption(
                    preferredDeviceId,
                    "保存済みのGPU",
                    0,
                    0,
                    false,
                    null,
                    false,
                    IsFallback: true),
            ];
        }

        var recommended = physical
            .Where(descriptor => descriptor.IsIntegrated == false
                || (descriptor.IsIntegrated is null && descriptor.DedicatedVideoMemoryBytes > 0))
            .OrderByDescending(descriptor => descriptor.DedicatedVideoMemoryBytes)
            .ThenBy(descriptor => descriptor.DeviceId)
            .FirstOrDefault();
        var recommendedId = recommended?.DeviceId ?? physical[0].DeviceId;

        var options = physical
            .Select(descriptor => new SukiMaaInferenceDeviceOption(
                descriptor.DeviceId,
                string.IsNullOrWhiteSpace(descriptor.Name) ? $"GPU {descriptor.DeviceId}" : descriptor.Name.Trim(),
                descriptor.DedicatedVideoMemoryBytes,
                descriptor.SharedSystemMemoryBytes,
                descriptor.IsSoftware,
                descriptor.IsIntegrated,
                descriptor.DeviceId == recommendedId))
            .ToList();

        if (options.All(option => option.DeviceId != preferredDeviceId))
        {
            options.Add(new SukiMaaInferenceDeviceOption(
                preferredDeviceId,
                "保存済みのGPU",
                0,
                0,
                false,
                null,
                false,
                IsFallback: true));
        }

        return options.OrderBy(option => option.DeviceId).ToArray();
    }

    private static IReadOnlyList<RhodesDxgiAdapterDescriptor> Enumerate()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DXGI adapter discovery is available only on Windows.");

        var iid = typeof(IDXGIFactory1).GUID;
        var result = CreateDXGIFactory1(ref iid, out var factory);
        Marshal.ThrowExceptionForHR(result);

        var descriptors = new List<RhodesDxgiAdapterDescriptor>();
        var coreFactory = TryCreateDxCoreFactory();
        try
        {
            for (uint index = 0; index <= 15; index++)
            {
                result = factory.EnumAdapters1(index, out var adapter);
                if (result == DxgiErrorNotFound)
                    break;
                Marshal.ThrowExceptionForHR(result);

                try
                {
                    result = adapter.GetDesc1(out var description);
                    Marshal.ThrowExceptionForHR(result);
                    descriptors.Add(new RhodesDxgiAdapterDescriptor(
                        (int)index,
                        description.Description ?? "",
                        description.DedicatedVideoMemory.ToUInt64(),
                        description.SharedSystemMemory.ToUInt64(),
                        (description.Flags & DxgiAdapterFlagSoftware) != 0,
                        ResolveIntegratedProperty(coreFactory, adapter, description.AdapterLuid)));
                }
                finally
                {
                    Marshal.FinalReleaseComObject(adapter);
                }
            }
        }
        finally
        {
            if (coreFactory is not null)
                Marshal.FinalReleaseComObject(coreFactory);
            Marshal.FinalReleaseComObject(factory);
        }

        return descriptors;
    }

    private static bool? ResolveIntegratedProperty(
        IDXCoreAdapterFactory? coreFactory,
        IDXGIAdapter1 adapter,
        Luid adapterLuid)
    {
        var uma = TryReadUmaProperty(adapter);
        var dxCore = TryReadIntegratedProperty(coreFactory, adapterLuid);
        if (uma == true || dxCore == true)
            return true;
        return uma ?? dxCore;
    }

    private static bool? TryReadUmaProperty(IDXGIAdapter1 adapter)
    {
        ID3D12Device? device = null;
        try
        {
            var iid = typeof(ID3D12Device).GUID;
            var result = D3D12CreateDevice(adapter, 0xB000, ref iid, out device);
            if (result < 0 || device is null)
                return null;

            var architecture = new D3D12FeatureDataArchitecture1 { NodeIndex = 0 };
            var size = Marshal.SizeOf<D3D12FeatureDataArchitecture1>();
            var data = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(architecture, data, false);
                result = device.CheckFeatureSupport(16, data, (uint)size);
                if (result < 0)
                    return null;
                architecture = Marshal.PtrToStructure<D3D12FeatureDataArchitecture1>(data);
                return architecture.Uma != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException
                                   or EntryPointNotFoundException
                                   or COMException
                                   or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (device is not null && OperatingSystem.IsWindows())
                Marshal.FinalReleaseComObject(device);
        }
    }

    private static IDXCoreAdapterFactory? TryCreateDxCoreFactory()
    {
        try
        {
            var iid = typeof(IDXCoreAdapterFactory).GUID;
            var result = DXCoreCreateAdapterFactory(ref iid, out var factory);
            return result >= 0 ? factory : null;
        }
        catch (Exception ex) when (ex is DllNotFoundException
                                   or EntryPointNotFoundException
                                   or COMException
                                   or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static bool? TryReadIntegratedProperty(IDXCoreAdapterFactory? factory, Luid adapterLuid)
    {
        if (factory is null)
            return null;

        IDXCoreAdapter? adapter = null;
        try
        {
            var iid = typeof(IDXCoreAdapter).GUID;
            var result = factory.GetAdapterByLuid(ref adapterLuid, ref iid, out adapter);
            if (result < 0 || adapter is null || !adapter.IsPropertySupported(12))
                return null;

            var value = Marshal.AllocHGlobal(1);
            try
            {
                Marshal.WriteByte(value, 0);
                result = adapter.GetProperty(12, (UIntPtr)1, value);
                return result >= 0 ? Marshal.ReadByte(value) != 0 : null;
            }
            finally
            {
                Marshal.FreeHGlobal(value);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            if (adapter is not null && OperatingSystem.IsWindows())
                Marshal.FinalReleaseComObject(adapter);
        }
    }

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 factory);

    [DllImport("dxcore.dll", ExactSpelling = true)]
    private static extern int DXCoreCreateAdapterFactory(
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IDXCoreAdapterFactory factory);

    [DllImport("d3d12.dll", ExactSpelling = true)]
    private static extern int D3D12CreateDevice(
        [MarshalAs(UnmanagedType.IUnknown)] object adapter,
        int minimumFeatureLevel,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out ID3D12Device device);

    [ComImport]
    [Guid("770AAE78-F26F-4DBA-A829-253C83D1B387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        [PreserveSig]
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

        [PreserveSig]
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

        [PreserveSig]
        int GetParent(ref Guid riid, out IntPtr parent);

        [PreserveSig]
        int EnumAdapters(uint adapter, out IntPtr adapterPointer);

        [PreserveSig]
        int MakeWindowAssociation(IntPtr windowHandle, uint flags);

        [PreserveSig]
        int GetWindowAssociation(out IntPtr windowHandle);

        [PreserveSig]
        int CreateSwapChain(IntPtr device, IntPtr description, out IntPtr swapChain);

        [PreserveSig]
        int CreateSoftwareAdapter(IntPtr moduleHandle, out IntPtr adapter);

        [PreserveSig]
        int EnumAdapters1(uint adapter, out IDXGIAdapter1 adapterPointer);

        [PreserveSig]
        int IsCurrent();
    }

    [ComImport]
    [Guid("29038F61-3839-4626-91FD-086879011A05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        [PreserveSig]
        int SetPrivateData(ref Guid name, uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateDataInterface(ref Guid name, IntPtr unknown);

        [PreserveSig]
        int GetPrivateData(ref Guid name, ref uint dataSize, IntPtr data);

        [PreserveSig]
        int GetParent(ref Guid riid, out IntPtr parent);

        [PreserveSig]
        int EnumOutputs(uint output, out IntPtr outputPointer);

        [PreserveSig]
        int GetDesc(out DxgiAdapterDescription description);

        [PreserveSig]
        int CheckInterfaceSupport(ref Guid interfaceName, out long userModeDriverVersion);

        [PreserveSig]
        int GetDesc1(out DxgiAdapterDescription1 description);
    }

    [ComImport]
    [Guid("78EE5945-C36E-4B13-A669-005DD11C0F06")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXCoreAdapterFactory
    {
        [PreserveSig]
        int CreateAdapterList(uint attributeCount, IntPtr filterAttributes, ref Guid riid, out IntPtr adapterList);

        [PreserveSig]
        int GetAdapterByLuid(
            ref Luid adapterLuid,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IDXCoreAdapter adapter);
    }

    [ComImport]
    [Guid("F0DB4C7F-FE5A-42A2-BD62-F2A6CF6FC83E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXCoreAdapter
    {
        [return: MarshalAs(UnmanagedType.I1)]
        bool IsValid();

        [return: MarshalAs(UnmanagedType.I1)]
        bool IsAttributeSupported(ref Guid attributeGuid);

        [return: MarshalAs(UnmanagedType.I1)]
        bool IsPropertySupported(uint property);

        [PreserveSig]
        int GetProperty(uint property, UIntPtr bufferSize, IntPtr propertyData);
    }

    [ComImport]
    [Guid("189819F1-1DB6-4B57-BE54-1821339B85F7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D12Device
    {
        [PreserveSig]
        int GetPrivateData(ref Guid guid, ref uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateData(ref Guid guid, uint dataSize, IntPtr data);

        [PreserveSig]
        int SetPrivateDataInterface(ref Guid guid, IntPtr data);

        [PreserveSig]
        int SetName([MarshalAs(UnmanagedType.LPWStr)] string name);

        uint GetNodeCount();

        [PreserveSig]
        int CreateCommandQueue(IntPtr description, ref Guid riid, out IntPtr commandQueue);

        [PreserveSig]
        int CreateCommandAllocator(uint type, ref Guid riid, out IntPtr commandAllocator);

        [PreserveSig]
        int CreateGraphicsPipelineState(IntPtr description, ref Guid riid, out IntPtr pipelineState);

        [PreserveSig]
        int CreateComputePipelineState(IntPtr description, ref Guid riid, out IntPtr pipelineState);

        [PreserveSig]
        int CreateCommandList(
            uint nodeMask,
            uint type,
            IntPtr commandAllocator,
            IntPtr initialState,
            ref Guid riid,
            out IntPtr commandList);

        [PreserveSig]
        int CheckFeatureSupport(uint feature, IntPtr featureSupportData, uint featureSupportDataSize);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public Luid AdapterLuid;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DxgiAdapterDescription1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string? Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public UIntPtr DedicatedVideoMemory;
        public UIntPtr DedicatedSystemMemory;
        public UIntPtr SharedSystemMemory;
        public Luid AdapterLuid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D12FeatureDataArchitecture1
    {
        public uint NodeIndex;
        public int TileBasedRenderer;
        public int Uma;
        public int CacheCoherentUma;
        public int IsolatedMmu;
    }
}
