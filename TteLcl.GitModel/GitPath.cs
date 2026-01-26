using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TteLcl.GitModel;

/// <summary>
/// A "path" object, representing a path from the repository root to a tree or blob.
/// While this is a <see cref="GitItem"/>, GIT itself does not know about these;
/// instead, this is a new concept introduced in this library, providing an explicit
/// representation of the paths implicit in tree objects.
/// </summary>
public class GitPath: GitItem
{
  private const string __pathSeparator = "/";

  /// <summary>
  /// Create a new <see cref="GitPath"/> instance.
  /// </summary>
  /// <param name="pathCache">
  /// The <see cref="GitPathCache"/> that creates and owns this <see cref="GitPath"/>
  /// instance.
  /// </param>
  /// <param name="path">
  /// The path that this instance represents (using '/' as path separator)
  /// </param>
  internal GitPath(GitPathCache pathCache, string path)
    : base(pathCache.IdCache, pathCache.IdCache.ForPath(path))
  {
    Owner = pathCache;
    Path = path;
  }

  /// <summary>
  /// The <see cref="GitPathCache"/> that created this <see cref="GitPath"/>.
  /// </summary>
  public GitPathCache Owner { get; }

  /// <summary>
  /// The '/' separated path, relative to the repository root.
  /// </summary>
  public string Path { get; }

  /// <summary>
  /// Retrieve or create a child path
  /// </summary>
  /// <param name="segment">
  /// The child path relative to this <see cref="GitPath"/> instance
  /// </param>
  /// <returns></returns>
  /// <exception cref="ArgumentNullException"></exception>
  /// <exception cref="ArgumentException"></exception>
  public GitPath Child(string segment)
  {
    if(String.IsNullOrEmpty(segment))
    {
      throw new ArgumentException(
        "Empty path segments are not valid (except as root)",
        nameof(segment));
    }
    if(segment.Contains('/'))
    {
      throw new ArgumentException(
        "A child path segment name must not contain path separators",
        nameof(segment));
    }
    var childPath = Path.Length == 0 ? segment : Path + __pathSeparator + segment;
    return Owner.GetPath(childPath);
  }

}
