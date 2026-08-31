using System;

namespace ExtremePowers.API
{
    public static class ExtremePowersBootstrap
    {
        private static readonly object Gate = new object();
        private static IExtremePowersApi instance;
        public static IExtremePowersApi Instance => instance ?? throw new InvalidOperationException("ExtremePowers API has not been initialized.");
        public static IExtremePowersApi Initialize(string crusaderDllPath)
        {
            lock (Gate) return instance ?? (instance = new ExtremePowersApi(crusaderDllPath));
        }

        public static IExtremePowersApi Initialize(string crusaderDllPath, IntPtr libraryHandle, ReadOnlySpan<byte> libraryMemory)
            => Initialize(crusaderDllPath, libraryHandle, libraryMemory, null);

        public static IExtremePowersApi Initialize(string crusaderDllPath, IntPtr libraryHandle, ReadOnlySpan<byte> libraryMemory, ExtremePowersBootstrapOptions options)
        {
            lock (Gate)
            {
                if (instance != null) return instance;
                instance = new ExtremePowersApi(crusaderDllPath, libraryHandle, libraryMemory, options);
                return instance;
            }
        }
    }
}
