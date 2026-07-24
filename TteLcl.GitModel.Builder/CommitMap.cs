using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace TteLcl.GitModel.Builder;

/// <summary>
/// A mapping from full commit ids to <see cref="Commit"/> instances
/// </summary>
public class CommitMap
{
  private readonly Dictionary<string, Commit> _map;

  /// <summary>
  /// Create a new empty <see cref="CommitMap"/>.
  /// </summary>
  public CommitMap()
  {
    _map = new Dictionary<string, Commit>();
  }

  /// <summary>
  /// Create a new <see cref="CommitMap"/> and add all the given <paramref name="commits"/>.
  /// </summary>
  /// <param name="commits"></param>
  /// <returns></returns>
  public static CommitMap FromCommits(IEnumerable<Commit> commits)
  {
    var commitMap = new CommitMap();
    commitMap.AddRange(commits);
    return commitMap;
  }

  /// <summary>
  /// A read-only view on the underlying map
  /// </summary>
  public IReadOnlyDictionary<string, Commit> Commits => _map;

  /// <summary>
  /// Calculate the set of commits in this <see cref="CommitMap"/> that are not referenced by
  /// other commits in this map, and return their IDs
  /// </summary>
  /// <returns></returns>
  public HashSet<string> TipIds()
  {
    // start with all ids
    var tipset = _map.Keys.ToHashSet();
    // then remove any referenced ones
    foreach(var commit in _map.Values)
    {
      foreach(var parent in commit.Parents)
      {
        tipset.Remove(parent.Sha);
      }
    }
    return tipset;
  }

  /// <summary>
  /// Calculate the set of parents referenced from commits in this <see cref="CommitMap"/> that
  /// are not in this map themselves, and return their IDs.
  /// Note that the returned set may include commits that are reachable from other commits in the returned
  /// set (i.e. it is not guaranteed to be minimal)
  /// </summary>
  /// <returns></returns>
  public HashSet<string> TailIds()
  {
    var tailset = new HashSet<string>();
    foreach(var commit in _map.Values)
    {
      foreach(var parent in commit.Parents)
      {
        if(!_map.ContainsKey(parent.Sha))
        {
          tailset.Add(parent.Sha);
        }
      }
    }
    return tailset;
  }

  /// <summary>
  /// Add (or replace) an entry
  /// </summary>
  /// <param name="commit"></param>
  public void Add(Commit commit)
  {
    _map[commit.Sha] = commit;
  }

  /// <summary>
  /// Add all of the <paramref name="commits"/> into this map
  /// </summary>
  /// <param name="commits"></param>
  public void AddRange(IEnumerable<Commit> commits)
  {
    foreach(var commit in commits)
    {
      _map[commit.Sha] = commit;
    }
  }

  /// <summary>
  /// Find the commit with the given id in this <see cref="CommitMap"/>, returning
  /// null if not found
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  public Commit? Find(string sha)
  {
    return _map.TryGetValue(sha, out var commit) ? commit : null;
  }

  /// <summary>
  /// Returns true if this <see cref="CommitMap"/> contains a commit with the given id.
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  public bool Contains(string sha)
  {
    return _map.ContainsKey(sha);
  }

  /// <summary>
  /// Returns tru if this <see cref="CommitMap"/> contains a commit with the same
  /// id as <paramref name="commit"/> (even if it is a different instance).
  /// </summary>
  /// <param name="commit"></param>
  /// <returns></returns>
  public bool Contains(Commit commit)
  {
    return _map.ContainsKey(commit.Sha);
  }

}
