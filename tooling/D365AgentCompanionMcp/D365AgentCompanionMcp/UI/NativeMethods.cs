// <copyright file="NativeMethods.cs" company="Luciano Spiguel">
// Copyright (c) Luciano Spiguel. All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>

namespace Lspiguel.Xrm.D365AgentCompanionMcp.UI
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>Flashes XrmToolBox's main window when a dialog needs the person — instead of guessing from <c>Visible</c>, which a background tab still reports as <c>true</c> (S6).</summary>
    internal static class NativeMethods
    {
        private const uint FlashAll = 3;
        private const uint FlashUntilForeground = 12;

        /// <summary>Flashes a window's caption and taskbar button until it comes to the foreground.</summary>
        /// <param name="window">The window handle.</param>
        public static void Flash(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return;
            }

            var info = new FlashInfo
            {
                Size = (uint)Marshal.SizeOf(typeof(FlashInfo)),
                Window = window,
                Flags = FlashAll | FlashUntilForeground,
                Count = uint.MaxValue,
                Timeout = 0,
            };
            FlashWindowEx(ref info);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FlashInfo info);

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashInfo
        {
            public uint Size;
            public IntPtr Window;
            public uint Flags;
            public uint Count;
            public uint Timeout;
        }
    }
}
