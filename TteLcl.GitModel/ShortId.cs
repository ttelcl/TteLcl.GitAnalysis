/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TteLcl.GitModel;

/// <summary>
/// An shortened identifier for an item in a git repository. The intention is
/// that this still uniquely identifies a git object, despite taking only 64 bits.
/// Logically this struct is equivalent to <see cref="UInt64"/>, and can implicitly
/// be converted to it
/// </summary>
public readonly struct ShortId: IEquatable<ShortId>, IComparable<ShortId>, IComparable
{
  /// <summary>
  /// Create a new <see cref="ShortId"/> from its <see cref="UInt64"/> representation
  /// </summary>
  public ShortId(ulong key)
  {
    Key = key;
  }

  /// <summary>
  /// Create a new <see cref="ShortId"/> from a signed interpretation of its key.
  /// At heart a <see cref="ShortId"/> is just 64 random bits, and whether those
  /// are represented as <see cref="UInt64"/> or <see cref="Int64"/> doesn't really
  /// matter.
  /// </summary>
  /// <param name="signedKey"></param>
  public ShortId(long signedKey)
  {
    Key = (ulong)signedKey;
  }

  /// <summary>
  /// Create a <see cref="ShortId"/> from the first 8 bytes of the given span
  /// (interpreting them as big-endian)
  /// </summary>
  /// <exception cref="ArgumentException"></exception>
  public static ShortId FromHashBytes(ReadOnlySpan<byte> hashBytes)
  {
    if(hashBytes.Length < 8)
    {
      throw new ArgumentException(
        "Expecting at least 8 bytes as argument",
        nameof(hashBytes));
    }
    var ul = BinaryPrimitives.ReadUInt64BigEndian(hashBytes[0..8]);
    return new ShortId(ul);
  }

  /// <summary>
  /// Create a new <see cref="ShortId"/> from the first 16 characters (all hexadecimal)
  /// of the given argument.
  /// </summary>
  /// <param name="hexhashtext"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  public static ShortId FromHexPrefix(ReadOnlySpan<char> hexhashtext)
  {
    if(hexhashtext.Length < 16)
    {
      throw new ArgumentException(
        "Expecting at least 16 hexadecimal characters in the argument",
        nameof(hexhashtext));
    }
    var ul = UInt64.Parse(hexhashtext, NumberStyles.AllowHexSpecifier);
    return new ShortId(ul);
  }

  /// <summary>
  /// The ID as an unsigned long integer (64 bits).
  /// The implicit conversion to <see cref="UInt64"/> also returns this value.
  /// </summary>
  public ulong Key { get; }

  /// <summary>
  /// The ID as a signed long integer
  /// </summary>
  public long AsInteger => (long)Key;

  /// <summary>
  /// Implicit conversion of <see cref="ShortId"/> to <see cref="UInt64"/>.
  /// This is equivalent to <see cref="Key"/>.
  /// </summary>
  /// <param name="id"></param>
  public static implicit operator ulong(ShortId id)
  {
    return id.Key;
  }

  /// <inheritdoc/>
  public bool Equals(ShortId other)
  {
    return Key == other.Key;
  }

  /// <inheritdoc/>
  public int CompareTo(ShortId other)
  {
    return Key.CompareTo(other.Key);
  }

  /// <inheritdoc/>
  public int CompareTo(object? obj)
  {
    if(obj is ShortId other)
    {
      return Key.CompareTo(other.Key);
    }
    if(obj is ulong u)
    {
      return Key.CompareTo(u);
    }
    throw new NotSupportedException(
      "ShortIds can only be compared to other ShortIds and UInt64");
  }
}
