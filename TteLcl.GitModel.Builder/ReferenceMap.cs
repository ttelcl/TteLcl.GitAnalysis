using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace TteLcl.GitModel.Builder;

/// <summary>
/// A mapping from canonical reference names to <see cref="Reference"/> instances
/// </summary>
public sealed class ReferenceMap
{
  private readonly Dictionary<string, Reference> _references;

  /// <summary>
  /// Create an empty <see cref="ReferenceMap"/>
  /// </summary>
  public ReferenceMap()
  {
    _references = new Dictionary<string, Reference>();
  }

  /// <summary>
  /// Create a new <see cref="ReferenceMap"/> and add references from <paramref name="repo"/>
  /// to it. If <paramref name="refPrefix"/> is null or empty, all references are added,
  /// alse only those whose canonical name starts with the given prefix.
  /// </summary>
  /// <param name="repo"></param>
  /// <param name="refPrefix"></param>
  public ReferenceMap(GitRepo repo, string? refPrefix = null)
    : this()
  {
    AddRepository(repo, refPrefix);
  }

  /// <summary>
  /// A read-only view on the references in this map
  /// </summary>
  public IReadOnlyDictionary<string, Reference> References => _references;

  /// <summary>
  /// Add all or a subset of the references in the <paramref name="repo"/> to this map.
  /// </summary>
  /// <param name="repo">
  /// The repository instance (defined in this library)
  /// </param>
  /// <param name="refPrefix">
  /// If not null or empty: only add references whose canonical name starts with this prefix
  /// (else: add all)
  /// </param>
  public void AddRepository(GitRepo repo, string? refPrefix = null)
  {
    AddRepository(repo.Repo, refPrefix);
  }

  /// <summary>
  /// Add all or a subset of the references in the <paramref name="repo"/> to this map.
  /// </summary>
  /// <param name="repo">
  /// The repository instance (defined in LibGit2Sharp)
  /// </param>
  /// <param name="refPrefix">
  /// If not null or empty: only add references whose canonical name starts with this prefix
  /// (else: add all)
  /// </param>
  public void AddRepository(Repository repo, string? refPrefix = null)
  {
    if(String.IsNullOrEmpty(refPrefix))
    {
      AddRange(repo.Refs);
    }
    else
    {
      AddRange(repo.Refs.Where(r => r.CanonicalName.StartsWith(refPrefix)));
    }
  }

  /// <summary>
  /// Add zero or more references to this map
  /// </summary>
  /// <param name="references"></param>
  public void AddRange(IEnumerable<Reference> references)
  {
    foreach(Reference reference in references)
    {
      Add(reference);
    }
  }

  /// <summary>
  /// Add an individual <see cref="Reference"/> to this map
  /// </summary>
  /// <param name="reference"></param>
  public void Add(Reference reference)
  {
    _references.Add(reference.CanonicalName, reference);
  }
}
