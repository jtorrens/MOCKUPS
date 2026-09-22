using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class AppleWebViewSnapshot
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private const string SystemLibrary = "/usr/lib/libSystem.B.dylib";
    private const long RtldDefault = -2;
    private const long PngBitmapFileType = 4;
    private static readonly SnapshotCompletion Completion = CompleteSnapshot;
    private static readonly IntPtr CompletionPointer =
        Marshal.GetFunctionPointerForDelegate(Completion);
    private static readonly IntPtr BlockDescriptor = CreateBlockDescriptor();

    public static Task<byte[]?> CapturePngAsync(NativeWebView webView)
    {
        ArgumentNullException.ThrowIfNull(webView);
        if (!OperatingSystem.IsMacOS()
            || webView.TryGetPlatformHandle()
                is not IAppleWKWebViewPlatformHandle appleHandle
            || appleHandle.WKWebView == IntPtr.Zero)
        {
            return Task.FromResult<byte[]?>(null);
        }

        var completion = new TaskCompletionSource<byte[]?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stateHandle = GCHandle.Alloc(completion);
        var block = new BlockLiteral
        {
            Isa = Dlsym(new IntPtr(RtldDefault), "_NSConcreteStackBlock"),
            Invoke = CompletionPointer,
            Descriptor = BlockDescriptor,
            Context = GCHandle.ToIntPtr(stateHandle),
        };
        if (block.Isa == IntPtr.Zero)
        {
            stateHandle.Free();
            completion.SetResult(null);
            return completion.Task;
        }

        var stackBlock = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
        IntPtr copiedBlock = IntPtr.Zero;
        try
        {
            Marshal.StructureToPtr(block, stackBlock, fDeleteOld: false);
            copiedBlock = BlockCopy(stackBlock);
            if (copiedBlock == IntPtr.Zero)
            {
                stateHandle.Free();
                completion.SetResult(null);
                return completion.Task;
            }

            Send(
                appleHandle.WKWebView,
                Selector("takeSnapshotWithConfiguration:completionHandler:"),
                IntPtr.Zero,
                copiedBlock);
        }
        catch (Exception error)
        {
            if (stateHandle.IsAllocated)
            {
                stateHandle.Free();
            }
            completion.TrySetException(error);
        }
        finally
        {
            if (copiedBlock != IntPtr.Zero)
            {
                BlockRelease(copiedBlock);
            }
            Marshal.FreeHGlobal(stackBlock);
        }

        return completion.Task;
    }

    private static void CompleteSnapshot(
        IntPtr blockPointer,
        IntPtr image,
        IntPtr error)
    {
        var block = Marshal.PtrToStructure<BlockLiteral>(blockPointer);
        var stateHandle = GCHandle.FromIntPtr(block.Context);
        try
        {
            var completion = (TaskCompletionSource<byte[]?>?)stateHandle.Target;
            if (completion is null)
            {
                return;
            }

            completion.TrySetResult(
                image == IntPtr.Zero || error != IntPtr.Zero
                    ? null
                    : EncodePng(image));
        }
        catch (Exception snapshotError)
        {
            ((TaskCompletionSource<byte[]?>?)stateHandle.Target)
                ?.TrySetException(snapshotError);
        }
        finally
        {
            stateHandle.Free();
        }
    }

    private static byte[]? EncodePng(IntPtr image)
    {
        var tiffData = Send(image, Selector("TIFFRepresentation"));
        if (tiffData == IntPtr.Zero)
        {
            return null;
        }

        var bitmapImageRep = Send(
            ObjcGetClass("NSBitmapImageRep"),
            Selector("imageRepWithData:"),
            tiffData);
        var properties = Send(
            ObjcGetClass("NSDictionary"),
            Selector("dictionary"));
        var pngData = Send(
            bitmapImageRep,
            Selector("representationUsingType:properties:"),
            new IntPtr(PngBitmapFileType),
            properties);
        if (pngData == IntPtr.Zero)
        {
            return null;
        }

        var length = checked((int)SendUIntPtr(
            pngData,
            Selector("length")).ToUInt64());
        var bytes = Send(pngData, Selector("bytes"));
        if (length <= 0 || bytes == IntPtr.Zero)
        {
            return null;
        }

        var result = new byte[length];
        Marshal.Copy(bytes, result, 0, length);
        return result;
    }

    private static IntPtr CreateBlockDescriptor()
    {
        var descriptor = Marshal.AllocHGlobal(IntPtr.Size * 2);
        Marshal.WriteIntPtr(descriptor, 0, IntPtr.Zero);
        Marshal.WriteIntPtr(
            descriptor,
            IntPtr.Size,
            new IntPtr(Marshal.SizeOf<BlockLiteral>()));
        return descriptor;
    }

    private static IntPtr Selector(string name) => SelRegisterName(name);

    [StructLayout(LayoutKind.Sequential)]
    private struct BlockLiteral
    {
        public IntPtr Isa;
        public int Flags;
        public int Reserved;
        public IntPtr Invoke;
        public IntPtr Descriptor;
        public IntPtr Context;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SnapshotCompletion(
        IntPtr block,
        IntPtr image,
        IntPtr error);

    [DllImport(SystemLibrary, EntryPoint = "dlsym")]
    private static extern IntPtr Dlsym(IntPtr handle, string symbol);

    [DllImport(SystemLibrary, EntryPoint = "_Block_copy")]
    private static extern IntPtr BlockCopy(IntPtr block);

    [DllImport(SystemLibrary, EntryPoint = "_Block_release")]
    private static extern void BlockRelease(IntPtr block);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_getClass")]
    private static extern IntPtr ObjcGetClass(string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "sel_registerName")]
    private static extern IntPtr SelRegisterName(string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(IntPtr receiver, IntPtr selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(
        IntPtr receiver,
        IntPtr selector,
        IntPtr argument);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern IntPtr Send(
        IntPtr receiver,
        IntPtr selector,
        IntPtr firstArgument,
        IntPtr secondArgument);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern UIntPtr SendUIntPtr(
        IntPtr receiver,
        IntPtr selector);
}
