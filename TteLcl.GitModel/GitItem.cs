using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TteLcl.GitModel;

/// <summary>
/// An item representing a standard GIT concept (commit, tree, blob, tag) or one of this
/// library's extension items (path, pathtree, pathblob).
/// Each <see cref="GitItem"/> is uniquely identified by a <see cref="GitId"/>.
/// </summary>
public abstract class GitItem
{
  /// <summary>
  /// Create a new <see cref="GitItem"/>, identified by the given <paramref name="id"/>.
  /// </summary>
  /// <param name="idCache"></param>
  /// <param name="id"></param>
  protected GitItem(GitIdCache idCache, GitId id)
  {
    IdCache = idCache;
    Id = idCache.Normalize(id);
  }

  /// <summary>
  /// The Git ID cache associated with this object
  /// </summary>
  public GitIdCache IdCache { get; }

  /// <summary>
  /// The <see cref="GitId"/> representing this object's unique ID.
  /// </summary>
  public GitId Id { get; }

}
