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
/// Provides static methods to calculate the hashes used as identifiers in GIT.
/// Mostly intended for internal use by
/// <see cref="GitIdCache.ForContent(string, ReadOnlySpan{byte})"/> and
/// <see cref="GitIdCache.ForBlob(ReadOnlySpan{byte})"/>
/// </summary>
public static class GitHash
{
  /// <summary>
  /// Calculates the hash of the concatenation of the segments.
  /// Primarily intended for internal use.
  /// </summary>
  /// <param name="header"></param>
  /// <param name="content"></param>
  /// <param name="hash"></param>
  /// <returns></returns>
  public static void FromRawContent(
    ReadOnlySpan<byte> header,
    ReadOnlySpan<byte> content,
    Span<byte> hash)
  {
    if(hash.Length != 20)
    {
      throw new ArgumentException(
        "Expecting a 20 byte result buffer");
    }
    var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA1);
    hasher.AppendData(header);
    hasher.AppendData(content);
    if(hasher.GetHashAndReset(hash) != 20)
    {
      throw new InvalidOperationException(
        "Internal error: expecting SHA1 hasher to return 20 bytes");
    }
  }

  /// <summary>
  /// Calculates the hash of the concatenation of the segments.
  /// Primarily intended for internal use.
  /// </summary>
  public static byte[] FromRawContent(
    ReadOnlySpan<byte> header,
    ReadOnlySpan<byte> content)
  {
    var bytes = new byte[20];
    FromRawContent(header, content, bytes);
    return bytes;
  }

  /// <summary>
  /// Calculate the GIT hash for an object with the given
  /// <paramref name="type"/> and <paramref name="content"/>.
  /// Primarily for invocation via <see cref="GitIdCache.ForContent(string, ReadOnlySpan{byte})"/>.
  /// </summary>
  /// <param name="type"></param>
  /// <param name="content"></param>
  /// <param name="hash"></param>
  /// <exception cref="InvalidOperationException"></exception>
  public static void FromContent(
    string type,
    ReadOnlySpan<byte> content,
    Span<byte> hash)
  {
    var length = content.Length;
    Span<byte> headerBytes = stackalloc byte[32];
    var n = Encoding.ASCII.GetBytes(type, headerBytes);
    headerBytes[n] = (byte)' ';
    if(!length.TryFormat(headerBytes[(n+1)..], out var lengthlength))
    {
      throw new InvalidOperationException("internal error");
    }
    var headerLength = n + 1 + lengthlength + 1;
    headerBytes[headerLength-1] = 0; // NUL byte to terminate the header
    headerBytes = headerBytes[0..headerLength];
    FromRawContent(headerBytes, content, hash);
  }

  /// <summary>
  /// Calculate the GIT hash for an object with the given
  /// <paramref name="type"/> and <paramref name="content"/>.
  /// Primarily intended for internal use.
  /// </summary>
  public static byte[] FromContent(
    string type,
    ReadOnlySpan<byte> content)
  {
    var bytes = new byte[20];
    FromContent(type, content, bytes);
    return bytes;
  }

  /// <summary>
  /// Calculate the GIT hash for an object with the type
  /// "blob" and the given <paramref name="content"/>.
  /// Primarily for invocation via <see cref="GitIdCache.ForBlob(ReadOnlySpan{byte})"/>.
  /// </summary>
  /// <param name="content"></param>
  /// <param name="hash"></param>
  public static void FromContent(
    ReadOnlySpan<byte> content,
    Span<byte> hash)
  {
    FromContent("blob", content, hash);
  }

  /// <summary>
  /// Calculate the GIT hash for an object with the type
  /// "blob" and the given <paramref name="content"/>.
  /// Primarily intended for internal use.
  /// </summary>
  public static byte[] FromContent(
    ReadOnlySpan<byte> content)
  {
    var bytes = new byte[20];
    FromContent(content, bytes);
    return bytes;
  }

  /// <summary>
  /// Convert a byte arry containing an SHA1 hash into a GitId
  /// Primarily intended for internal use.
  /// </summary>
  /// <param name="hash"></param>
  /// <returns></returns>
  public static GitId ToGitId(this byte[] hash)
  {
    return new GitId(hash);
  }
}
