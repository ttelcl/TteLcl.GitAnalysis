/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TteLcl.GitModel;

/// <summary>
/// Tracks a GIT object identifier in full and shortened forms. 
/// </summary>
public class GitId
{
  private readonly byte[] _bytes;

  /// <summary>
  /// Create a new <see cref="GitId"/> instance from its 20 byte binary
  /// representation.
  /// </summary>
  /// <param name="idBytes"></param>
  /// <exception cref="ArgumentException"></exception>
  public GitId(ReadOnlySpan<byte> idBytes)
  {
    if(idBytes.Length != 20)
    {
      throw new ArgumentException(
        "Expecting precisely 20 bytes as argument",
        nameof(idBytes));
    }
    _bytes = idBytes.ToArray();
    FullString = Convert.ToHexString(idBytes).ToLowerInvariant();
    Id = ShortId.FromHashBytes(idBytes);
  }

  /// <summary>
  /// Create a <see cref="GitId"/> from a 40 character hexadecimal string.
  /// The current implementation allocates a temporary byte buffer.
  /// </summary>
  /// <param name="sha1hextext"></param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  /// <exception cref="InvalidOperationException"></exception>
  public static GitId FromString(ReadOnlySpan<char> sha1hextext)
  {
    if(sha1hextext.Length != 40)
    {
      throw new ArgumentException(
        "Expecting a 40 character hexadecimal string as input",
        nameof(sha1hextext));
    }
    // We assume net8 as baseline, so there is no allocation free version of
    // Convert.FromHexString. For now use the allocating one.
    var bytes = Convert.FromHexString(sha1hextext);
    if(bytes.Length != 20)
    {
      throw new InvalidOperationException(
        "The input doesn't behave as a hexadecimal string");
    }
    return new GitId(bytes);
  }

  /// <summary>
  /// Return the zero GitId.
  /// </summary>
  public static GitId Zero { get; } = new GitId(new byte[20]);

  /// <summary>
  /// The full 40 character string representation of the id (lowercase hexadecimal).
  /// </summary>
  public string FullString { get; }

  /// <summary>
  /// The short ID for this <see cref="GitId"/>. It is expected that in practice this
  /// will always uniquely identify this instance.
  /// </summary>
  public ShortId Id { get; }

  /// <summary>
  /// A view on the binary form of this ID (20 bytes)
  /// </summary>
  public ReadOnlySpan<byte> Binary => _bytes;

  /// <summary>
  /// Copy the <see cref="Binary"/> form to the <paramref name="destination"/> span
  /// </summary>
  /// <param name="destination">
  /// A 20 byte buffer to copy the binary git id to.
  /// </param>
  public void CopyTo(Span<byte> destination)
  {
    _bytes.CopyTo(destination);
  }
}
