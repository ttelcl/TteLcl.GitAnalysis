using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace TteLcl.GitModel.Builder;

/// <summary>
/// A collection of mappings from reference names to <see cref="Commit"/>s. This is similar
/// to <see cref="ReferenceMap"/>, but with References resolved to the Commit they refer to
/// (directly, symbolicly, or via a tag annotation), and non-commit references removed.
/// </summary>
public class CommitReferenceMap
{
  private readonly Dictionary<string, Commit> _map;
  private IReadOnlyDictionary<string, IReadOnlyList<string>>? _reverseMap = null;

  /// <summary>
  /// Create a new <see cref="CommitReferenceMap"/> from the given set of references
  /// </summary>
  /// <param name="references"></param>
  public CommitReferenceMap(IEnumerable<Reference> references)
  {
    _map = new Dictionary<string, Commit>();
    foreach(Reference reference in references)
    {
      var commit = reference.TryResolveToCommit();
      if(commit != null)
      {
        _map[reference.CanonicalName] = commit;
      }
    }
  }

  /// <summary>
  /// A read-only view on the mapping of full reference names to <see cref="Commit"/>s.
  /// </summary>
  public IReadOnlyDictionary<string, Commit> CommitsByReference => _map;

  /// <summary>
  /// Return the reverse mapping, mapping commit IDs to a list of full reference names.
  /// This value is lazily calculated on first access.
  /// </summary>
  public IReadOnlyDictionary<string, IReadOnlyList<string>> ReferencesByCommit {
    get {
      if(_reverseMap == null)
      {
        _reverseMap = CalculateReferencesByCommit();
      }
      return _reverseMap;
    }
  }

  /// <summary>
  /// Return the list of full reference names that point to the commit with the given id.
  /// Returns an empty list if no references are known for the commit.
  /// If not already done so, this will hydrate <see cref="ReferencesByCommit"/> as side effect.
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  public IReadOnlyList<string> ReferencesForCommit(string sha)
  {
    if(ReferencesByCommit.TryGetValue(sha, out var references))
    {
      return references;
    }
    else
    {
      return [];
    }
  }

  /// <summary>
  /// Calculate the reverse mapping: mapping commit IDs to a list of full reference names
  /// </summary>
  /// <returns></returns>
  private IReadOnlyDictionary<string, IReadOnlyList<string>> CalculateReferencesByCommit()
  {
    return
      _map
      .GroupBy(kvp => kvp.Value.Sha, kvp => kvp.Key)
      .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.ToList());
  }
}
