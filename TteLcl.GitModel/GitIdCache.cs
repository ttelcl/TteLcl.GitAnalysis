/*
 * (c) 2026  ttelcl / ttelcl
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TteLcl.GitModel.Utilities;

namespace TteLcl.GitModel;

/// <summary>
/// Caches <see cref="GitId"/> instances and indexes them by <see cref="ShortId"/>.
/// </summary>
public class GitIdCache
{
  private readonly Dictionary<ShortId, GitId> _map;

  /// <summary>
  /// Create a new GitIdCache
  /// </summary>
  public GitIdCache()
  {
    _map = [];
    var zero = GitId.Zero;
    _map.Add(zero.Id, zero);
  }

  /// <summary>
  /// Look up the given <paramref name="key"/>, returning true if found (and the value in
  /// <paramref name="value"/>), or false if not found.
  /// </summary>
  /// <param name="key"></param>
  /// <param name="value"></param>
  /// <returns></returns>
  public bool TryGetValue(ShortId key, [MaybeNullWhen(false)] out GitId value)
  {
    return _map.TryGetValue(key, out value);
  }

  /// <summary>
  /// Return the value for the given <paramref name="key"/>, throwing an
  /// exception if not found.
  /// </summary>
  /// <param name="key"></param>
  /// <returns></returns>
  /// <exception cref="KeyNotFoundException"></exception>
  public GitId this[ShortId key] {
    get => TryGetValue(key, out var gitId)
      ? gitId
      : throw new KeyNotFoundException(
        $"Git Key not found in this cache: 0x{key.Key:16x}");
  }

  /// <summary>
  /// Return the value for the given <paramref name="sha1"/> string. If not found,
  /// a new <see cref="GitId"/> is created and inserted (and returned).
  /// </summary>
  /// <param name="sha1">
  /// The full 40 character hexadecimal key to look up.
  /// </param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  public GitId this[ReadOnlySpan<char> sha1] {
    get {
      if(sha1.Length != 40)
      {
        // Ensure consistent behaviour. Without this test this accessor would
        // return an existing value but fail to insert a missing value.
        throw new ArgumentException(
          "Expecting a 40 character hexadecimal string as key",
          nameof(sha1));
      }
      var shortId = ShortId.FromHexPrefix(sha1);
      if(TryGetValue(shortId, out var gitId))
      {
        return gitId;
      }
      gitId = GitId.FromString(sha1);
      _map.Add(shortId, gitId);
      return gitId;
    }
  }

  /// <summary>
  /// Return the <see cref="GitId"/> for the given <paramref name="sha1"/> binary representation.
  /// If not already registered, it will be created and inserted.
  /// </summary>
  /// <param name="sha1">
  /// The 20 byte SHA1 hash that the GitId represents
  /// </param>
  /// <returns></returns>
  /// <exception cref="ArgumentException"></exception>
  public GitId this[ReadOnlySpan<byte> sha1] {
    get {
      if(sha1.Length != 20)
      {
        throw new ArgumentException(
          "Expecting a 20 byte hash result as argument",
          nameof(sha1));
      }
      var shortId = ShortId.FromHashBytes(sha1);
      if(TryGetValue(shortId, out var gitId))
      {
        return gitId;
      }
      gitId = new GitId(sha1);
      _map.Add(shortId, gitId);
      return gitId;
    }
  }

  /// <summary>
  /// If <paramref name="gitId"/> is already in this cache: return the cached git id.
  /// Otherwise insert <paramref name="gitId"/> in this cache and return it.
  /// </summary>
  /// <param name="gitId"></param>
  /// <returns></returns>
  public GitId Normalize(GitId gitId)
  {
    if(TryGetValue(gitId.Id, out var gitId2))
    {
      // An instance already existed
      return gitId2;
    }
    _map.Add(gitId.Id, gitId);
    return gitId;
  }

  /// <summary>
  /// Calculate the git object hash for the given object <paramref name="type"/>
  /// and binary <paramref name="content"/>,
  /// and return the corresponding <see cref="GitId"/> for the result
  /// (inserting it in this cache if not yet present)
  /// </summary>
  /// <param name="type">
  /// The git object type ("blob", "tree", "commit" or "tag"), or your custom
  /// type identifier.
  /// </param>
  /// <param name="content">
  /// The raw content of the object. If <paramref name="type"/> is other than
  /// "blob", the format should be as prescribed by the type.
  /// </param>
  /// <returns></returns>
  public GitId ForContent(string type, ReadOnlySpan<byte> content)
  {
    Span<byte> hash = stackalloc byte[20];
    GitHash.FromContent(type, content, hash);
    var shortId = ShortId.FromHashBytes(hash);
    if(TryGetValue(shortId, out var gitId))
    {
      return gitId;
    }
    gitId = new GitId(hash);
    _map.Add(shortId, gitId);
    return gitId;
  }

  /// <summary>
  /// Calculate the git object hash for the given object <paramref name="type"/>
  /// and textual <paramref name="utf8content"/>,
  /// and return the corresponding <see cref="GitId"/> for the result
  /// (inserting it in this cache if not yet present)
  /// </summary>
  /// <param name="type">
  /// The git object type ("blob", "tree", "commit" or "tag"), or your custom
  /// type identifier.
  /// </param>
  /// <param name="utf8content">
  /// The raw content of the object as a string to be UTF8 encoded.
  /// </param>
  /// <returns></returns>
  public GitId ForContent(string type, string utf8content)
  {
    return ForContent(type, Encoding.UTF8.GetBytes(utf8content));
  }

  /// <summary>
  /// Calculate the git object hash for the given blob,
  /// and return the corresponding <see cref="GitId"/> for the result
  /// (inserting it in this cache if not yet present)
  /// </summary>
  /// <param name="blob">
  /// The blob content
  /// </param>
  /// <returns></returns>
  public GitId ForBlob(ReadOnlySpan<byte> blob)
  {
    return ForContent("blob", blob);
  }

  /// <summary>
  /// Calculate the git object hash for the given textual blob <paramref name="utf8content"/>,
  /// and return the corresponding <see cref="GitId"/> for the result
  /// (inserting it in this cache if not yet present)
  /// </summary>
  /// <param name="utf8content">
  /// The blob content, which will be UTF8 encoded.
  /// </param>
  /// <returns></returns>
  public GitId ForBlob(string utf8content)
  {
    return ForContent("blob", utf8content);
  }

  /// <summary>
  /// Create and store a <see cref="GitId"/> for an ordered pair of two existing GitIds.
  /// </summary>
  /// <param name="type">
  /// The object type name to use, e.g. "pathblob" or "pathtree"
  /// </param>
  /// <param name="pathId">
  /// The first <see cref="GitId"/>. Presumably a path id.
  /// </param>
  /// <param name="itemId">
  /// The second <see cref="GitId"/>. Presumably a tree id or blob id.
  /// </param>
  /// <returns></returns>
  public GitId Compose(string type, GitId pathId, GitId itemId)
  {
    Span<byte> buffer = stackalloc byte[40];
    pathId.CopyTo(buffer[0..20]);
    itemId.CopyTo(buffer[20..]);
    return ForContent(type, buffer);
  }

  /// <summary>
  /// Create or look up a <see cref="GitId"/> for a "path" with value <paramref name="path"/>.
  /// Path objects are not a formal GIT concept, but are used in this library to represent
  /// the location of blobs and trees.
  /// </summary>
  /// <param name="path"></param>
  /// <returns></returns>
  public GitId ForPath(string path)
  {
    return ForContent("path", path);
  }

  /// <summary>
  /// A view on all the cached GitId instances
  /// </summary>
  public IReadOnlyCollection<GitId> Entries => _map.Values;
}
