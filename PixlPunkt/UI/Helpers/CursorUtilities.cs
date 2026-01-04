//#nullable enable
//using System;
//using System.ComponentModel;
//using System.Diagnostics.CodeAnalysis;
//using System.Runtime.InteropServices;
//using Microsoft.UI.Input;
//using Microsoft.UI.Xaml;

//namespace PixlPunkt.UI.Helpers;

///// <summary>
///// Provides utilities for loading custom cursor files and applying them to XAML elements in WinUI 3.
///// </summary>
//public static class CursorUtilities
//{
//    // DynamicDependency ensures ProtectedCursor property is preserved during trimming
//    [DynamicDependency("ProtectedCursor", typeof(UIElement))]
//    private static readonly System.Reflection.PropertyInfo? ProtectedCursorProp =
//        typeof(UIElement).GetProperty("ProtectedCursor",
//            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

//    /// <summary>
//    /// Changes the cursor for a XAML element using reflection.
//    /// </summary>
//    /// <param name="element">Target UIElement to modify.</param>
//    /// <param name="cursor">InputCursor to apply, or null to reset to default.</param>
//    [DynamicDependency("ProtectedCursor", typeof(UIElement))]
//    public static void ChangeCursor(this UIElement element, InputCursor? cursor)
//    {
//        ArgumentNullException.ThrowIfNull(element);
//        ProtectedCursorProp?.SetValue(element, cursor);
//    }

//    /// <summary>
//    /// Loads a cursor from a .cur file on disk.
//    /// </summary>
//    public static InputCursor? LoadCursorFromFile(string filePath)
//    {
//        ArgumentNullException.ThrowIfNull(filePath);

//        nint hcursor = LoadCursorFromFileW(filePath);
//        if (hcursor == 0)
//            throw new Win32Exception(Marshal.GetLastWin32Error());

//        return CreateCursorFromHCURSOR(hcursor);
//    }

//    /// <summary>
//    /// Creates an InputCursor from a native cursor handle (HCURSOR).
//    /// </summary>
//    public static InputCursor? CreateCursorFromHCURSOR(nint hcursor)
//    {
//        if (hcursor == 0)
//            return null;

//        const string classId = "Microsoft.UI.Input.InputCursor";

//        WindowsCreateString(classId, classId.Length, out var hstr);
//        RoGetActivationFactory(hstr, typeof(IActivationFactory).GUID, out var factory);
//        WindowsDeleteString(hstr);

//        if (factory is not IInputCursorStaticsInterop interop)
//            return null;

//        interop.CreateFromHCursor(hcursor, out var cursorAbi);
//        if (cursorAbi == 0)
//            return null;

//        return WinRT.MarshalInspectable<InputCursor>.FromAbi(cursorAbi);
//    }

//    // --- interop gunk ---

//    [ComImport, Guid("ac6f5065-90c4-46ce-beb7-05e138e54117"),
//     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//    private interface IInputCursorStaticsInterop
//    {
//        void GetIids();
//        void GetRuntimeClassName();
//        void GetTrustLevel();

//        [PreserveSig]
//        int CreateFromHCursor(nint hcursor, out nint inputCursor);
//    }

//    [ComImport, Guid("00000035-0000-0000-c000-000000000046"),
//     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
//    private interface IActivationFactory
//    {
//        void GetIids();
//        void GetRuntimeClassName();
//        void GetTrustLevel();

//        [PreserveSig]
//        int ActivateInstance(out nint instance);
//    }

//    [DllImport("api-ms-win-core-winrt-l1-1-0.dll")]
//    private static extern int RoGetActivationFactory(
//        nint runtimeClassId,
//        [MarshalAs(UnmanagedType.LPStruct)] Guid iid,
//        out IActivationFactory factory);

//    [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
//    private static extern nint LoadCursorFromFileW(string name);

//    [DllImport("api-ms-win-core-winrt-string-l1-1-0", CharSet = CharSet.Unicode)]
//    private static extern int WindowsCreateString(string? sourceString, int length, out nint @string);

//    [DllImport("api-ms-win-core-winrt-string-l1-1-0", CharSet = CharSet.Unicode)]
//    private static extern int WindowsDeleteString(nint @string);
//}
