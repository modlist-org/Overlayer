using System;
using System.Runtime.InteropServices;

// CS8500 (address-of managed T): by construction only ever executed for blittable
// T (native spans, byte/char materializations). See project notes.
#pragma warning disable CS8500

namespace System.Runtime.CompilerServices {
    // Polyfills for compiling ref structs against netstandard2.0 refs.
    // Attribute-only use; never referenced by external callers (standard practice).
    internal sealed class IsByRefLikeAttribute : Attribute {
    }

    internal sealed class IsReadOnlyAttribute : Attribute {
    }
}

namespace System {
    /// <summary>GC-safe minimal <c>ReadOnlySpan&lt;T&gt;</c> (see project notes).</summary>
    public readonly ref struct ReadOnlySpan<T> {
        private readonly object _source; // T[] | string | null(native)
        private readonly IntPtr _ptr;    // native only
        private readonly int _off;       // element offset for managed sources
        private readonly int _len;

        public unsafe ReadOnlySpan(void* pointer, int length) {
            if(length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            _source = null;
            _ptr = (IntPtr)pointer;
            _off = 0;
            _len = length;
        }

        public ReadOnlySpan(T[] array) {
            if(array == null) {
                _source = null;
                _ptr = IntPtr.Zero;
                _off = 0;
                _len = 0;
                return;
            }
            _source = array;
            _ptr = IntPtr.Zero;
            _off = 0;
            _len = array.Length;
        }

        public ReadOnlySpan(T[] array, int start, int length) {
            if(array == null) {
                if(start != 0 || length != 0) {
                    throw new ArgumentOutOfRangeException();
                }
                _source = null;
                _ptr = IntPtr.Zero;
                _off = 0;
                _len = 0;
                return;
            }
            if(start < 0 || length < 0 || start + length > array.Length) {
                throw new ArgumentOutOfRangeException();
            }
            _source = array;
            _ptr = IntPtr.Zero;
            _off = start;
            _len = length;
        }

        public int Length => _len;

        public bool IsEmpty => _len == 0;

        public ref readonly T this[int index] {
            get {
                if((uint)index >= (uint)_len) {
                    throw new IndexOutOfRangeException();
                }
                if(_source is T[] array) {
                    return ref array[_off + index];
                }
                unsafe {
                    return ref *(T*)((byte*)_ptr.ToPointer() + (long)index * SizeOfT);
                }
            }
        }

        public ReadOnlySpan<T> Slice(int start) => Slice(start, _len - start);

        public ReadOnlySpan<T> Slice(int start, int length) {
            if(start < 0 || length < 0 || start + length > _len) {
                throw new ArgumentOutOfRangeException();
            }
            if(_source is T[] array) {
                return new ReadOnlySpan<T>(array, _off + start, length);
            }
            if(_source is string text) {
                // Defensive only: AsSpan(string) eagerly copies, so string sources
                // never actually occur. Materialize once if one ever does.
                return new ReadOnlySpan<T>((T[])(object)text.ToCharArray(), start, length);
            }
            unsafe {
                return new ReadOnlySpan<T>((void*)((byte*)_ptr.ToPointer() + (long)start * SizeOfT), length);
            }
        }

        public T[] ToArray() {
            if(_len == 0) {
                return new T[0];
            }
            var result = new T[_len];
            if(_source is T[] array) {
                Array.Copy(array, _off, result, 0, _len);
                return result;
            }
            if(_source is string text) {
                if(typeof(T) != typeof(char)) {
                    throw new NotSupportedException();
                }
                text.CopyTo(_off, (char[])(object)result, 0, _len);
                return result;
            }
            unsafe {
                var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
                try {
                    Buffer.MemoryCopy(
                        (void*)_ptr,
                        (void*)handle.AddrOfPinnedObject(),
                        (long)_len * SizeOfT,
                        (long)_len * SizeOfT);
                } finally {
                    handle.Free();
                }
            }
            return result;
        }

        private static int SizeOfT => SizeCache<T>.Value;

        private static class SizeCache<TSize> {
            public static readonly int Value = Marshal.SizeOf(typeof(TSize));
        }
    }

    /// <summary>GC-safe minimal <c>Span&lt;T&gt;</c> (see project notes).</summary>
    public ref struct Span<T> {
        private readonly object _source; // T[] | null(native)
        private readonly IntPtr _ptr;    // native only
        private readonly int _off;
        private readonly int _len;

        public unsafe Span(void* pointer, int length) {
            if(length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            _source = null;
            _ptr = (IntPtr)pointer;
            _off = 0;
            _len = length;
        }

        public Span(T[] array) {
            if(array == null) {
                _source = null;
                _ptr = IntPtr.Zero;
                _off = 0;
                _len = 0;
                return;
            }
            _source = array;
            _ptr = IntPtr.Zero;
            _off = 0;
            _len = array.Length;
        }

        public Span(T[] array, int start, int length) {
            if(array == null) {
                throw new ArgumentNullException(nameof(array));
            }
            if(start < 0 || length < 0 || start + length > array.Length) {
                throw new ArgumentOutOfRangeException();
            }
            _source = array;
            _ptr = IntPtr.Zero;
            _off = start;
            _len = length;
        }

        public int Length => _len;

        public bool IsEmpty => _len == 0;

        public ref T this[int index] {
            get {
                if((uint)index >= (uint)_len) {
                    throw new IndexOutOfRangeException();
                }
                if(_source is T[] array) {
                    return ref array[_off + index];
                }
                unsafe {
                    return ref *(T*)((byte*)_ptr.ToPointer() + (long)index * SizeOfT);
                }
            }
        }

        public Span<T> Slice(int start) => Slice(start, _len - start);

        public Span<T> Slice(int start, int length) {
            if(start < 0 || length < 0 || start + length > _len) {
                throw new ArgumentOutOfRangeException();
            }
            if(_source is T[] array) {
                return new Span<T>(array, _off + start, length);
            }
            unsafe {
                return new Span<T>((void*)((byte*)_ptr.ToPointer() + (long)start * SizeOfT), length);
            }
        }

        public T[] ToArray() {
            if(_len == 0) {
                return new T[0];
            }
            var result = new T[_len];
            if(_source is T[] array) {
                Array.Copy(array, _off, result, 0, _len);
                return result;
            }
            unsafe {
                var handle = GCHandle.Alloc(result, GCHandleType.Pinned);
                try {
                    Buffer.MemoryCopy(
                        (void*)_ptr,
                        (void*)handle.AddrOfPinnedObject(),
                        (long)_len * SizeOfT,
                        (long)_len * SizeOfT);
                } finally {
                    handle.Free();
                }
            }
            return result;
        }

        private static int SizeOfT => SizeCache<T>.Value;

        private static class SizeCache<TSize> {
            public static readonly int Value = Marshal.SizeOf(typeof(TSize));
        }
    }

    /// <summary>GC-safe minimal <c>MemoryExtensions</c> (see project notes).</summary>
    public static class MemoryExtensions {
        public static ReadOnlySpan<char> AsSpan(string text) {
            if(text == null) {
                return default;
            }
            return new ReadOnlySpan<char>(text.ToCharArray());
        }

        public static Span<T> AsSpan<T>(T[] array) => new Span<T>(array);

        public static Span<T> AsSpan<T>(T[] array, int start) {
            if(array == null) {
                throw new ArgumentNullException(nameof(array));
            }
            return new Span<T>(array, start, array.Length - start);
        }

        public static Span<T> AsSpan<T>(T[] array, int start, int length) => new Span<T>(array, start, length);
    }
}

namespace System.Runtime.InteropServices {
    /// <summary>GC-safe minimal <c>MemoryMarshal</c> (see project notes).</summary>
    public static class MemoryMarshal {
        public static ReadOnlySpan<byte> AsBytes<T>(ReadOnlySpan<T> span) {
            int count = span.Length;
            if(count == 0) {
                return default;
            }
            // Fast paths for the only reachable shapes: materialize little-endian.
            T[] items = span.ToArray();
            if(typeof(T) == typeof(byte)) {
                return new ReadOnlySpan<byte>((byte[])(object)items);
            }
            if(typeof(T) == typeof(char)) {
                char[] chars = (char[])(object)items;
                var bytes = new byte[chars.Length * 2];
                for(int i = 0; i < chars.Length; i++) {
                    bytes[i * 2] = (byte)chars[i];
                    bytes[i * 2 + 1] = (byte)(chars[i] >> 8);
                }
                return new ReadOnlySpan<byte>(bytes);
            }
            // Generic fallback: pinned reinterpret copy. Correct for any blittable T;
            // non-blittable T was never reachable and throws below.
            int size;
            try {
                size = Marshal.SizeOf(typeof(T));
            } catch {
                throw new NotSupportedException($"AsBytes not supported for {typeof(T)}.");
            }
            var raw = new byte[count * size];
            unsafe {
                var handle = GCHandle.Alloc(items, GCHandleType.Pinned);
                try {
                    fixed(byte* dst = raw) {
                        Buffer.MemoryCopy(
                            (void*)handle.AddrOfPinnedObject(),
                            dst,
                            raw.Length,
                            raw.Length);
                    }
                } finally {
                    handle.Free();
                }
            }
            return new ReadOnlySpan<byte>(raw);
        }
    }
}
