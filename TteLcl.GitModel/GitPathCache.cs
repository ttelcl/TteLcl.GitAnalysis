using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TteLcl.GitModel;

/// <summary>
/// A cache of <see cref="GitPath"/> instances.
/// To create and add <see cref="GitPath"/> instances, use the
/// <see cref="GitPath.Child(string)"/> method to add children to
/// existing instances, starting from <see cref="Root"/>.
/// </summary>
public class GitPathCache
{
  private readonly Dictionary<string, GitPath> _cache;

  /// <summary>
  /// Initialize a new <see cref="GitPathCache"/>.
  /// </summary>
  /// <param name="idCache"></param>
  public GitPathCache(GitIdCache idCache)
  {
    _cache = [];
    IdCache = idCache;
    Root = GetPath("");
  }

  /// <summary>
  /// The <see cref="IdCache"/> managing git IDs
  /// </summary>
  public GitIdCache IdCache { get; }

  /// <summary>
  /// The root path object (path ""). This is the starting point where
  /// you can add new paths (using the <see cref="GitPath.Child(string)"/>
  /// mathod).
  /// </summary>
  public GitPath Root { get; }

  /// <summary>
  /// Get the existing <see cref="GitPath"/> instance or create a new one.
  /// </summary>
  /// <param name="path"></param>
  /// <returns></returns>
  internal GitPath GetPath(string path)
  {
    if(!_cache.TryGetValue(path, out var gitPath)) {
      gitPath = new GitPath(this, path);
      _cache.Add(path, gitPath);
    }
    return gitPath;
  }
}
