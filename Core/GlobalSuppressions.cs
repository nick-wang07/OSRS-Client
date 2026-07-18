// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.SelectObject(System.IntPtr,System.IntPtr)~System.IntPtr")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.CreateCompatibleBitmap(System.IntPtr,System.Int32,System.Int32)~System.IntPtr")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.CreateCompatibleDC(System.IntPtr)~System.IntPtr")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.ReleaseDC(System.IntPtr,System.IntPtr)~System.Int32")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.GetWindowDC(System.IntPtr)~System.IntPtr")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.GetForegroundWindow~System.IntPtr")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "Yes they should", Scope = "member", Target = "~M:Core.Native.NativeMethods.GetSystemMetrics(System.Int32)~System.Int32")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "My computer is after windows 6.1", Scope = "member", Target = "~M:Core.src.Vision.FastPixelReader.GetPixel(System.Int32,System.Int32)~System.Drawing.Color")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>", Scope = "member", Target = "~M:Core.src.Vision.FastPixelReader.Dispose")]
[assembly: SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>", Scope = "member", Target = "~M:Core.src.Vision.FastPixelReader.#ctor(System.IntPtr,System.Drawing.Rectangle)")]
[assembly: SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "<Pending>", Scope = "member", Target = "~M:Core.Native.NativeMethods.GetDC(System.IntPtr)~System.IntPtr")]
