using System;
using System.Security.Cryptography;

namespace PhantomVault.Security.SecureBuffering;

/// <summary>
/// A disposable wrapper around sensitive cryptographic key bytes.
/// Ensures that key material is explicitly zeroized upon disposal using CryptographicOperations.ZeroMemory.
/// </summary>
public sealed class SecureBuffer : IDisposable
{
    private byte[]? _buffer;
    private bool _disposed;

    public SecureBuffer(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), "Buffer length must be greater than zero.");

        _buffer = new byte[length];
    }

    public SecureBuffer(byte[] sourceData, bool zeroizeSource = false)
    {
        ArgumentNullException.ThrowIfNull(sourceData);
        _buffer = (byte[])sourceData.Clone();

        if (zeroizeSource)
        {
            CryptographicOperations.ZeroMemory(sourceData);
        }
    }

    public byte[] DangerousGetBuffer()
    {
        ObjectDisposedException.ThrowIf(_disposed || _buffer == null, this);
        return _buffer;
    }

    public ReadOnlySpan<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed || _buffer == null, this);
            return _buffer.AsSpan();
        }
    }

    public int Length
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed || _buffer == null, this);
            return _buffer.Length;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_buffer != null)
            {
                CryptographicOperations.ZeroMemory(_buffer);
                _buffer = null;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    ~SecureBuffer()
    {
        Dispose();
    }
}
