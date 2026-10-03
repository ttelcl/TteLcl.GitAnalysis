using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace TteLcl.GitModel.Builder;

/// <summary>
/// Stores <see cref="CommitStub"/>s and their connections for a set of <see cref="Commit"/>s.
/// </summary>
public class CommitStubGraph
{
  private readonly Dictionary<string, CommitStub> _stubMap = new Dictionary<string, CommitStub>();

  /// <summary>
  /// Create a new empty <see cref="CommitStubGraph"/>.
  /// </summary>
  public CommitStubGraph()
  {
  }

  /// <summary>
  /// Create a new <see cref="CommitStubGraph"/>, and connect all the commits
  /// in <paramref name="commits"/> to it.
  /// </summary>
  /// <param name="commits"></param>
  public CommitStubGraph(IEnumerable<Commit> commits)
    : this()
  {
    ConnectAll(commits);
  }

  /// <summary>
  /// The mapping of full SHA ids to their commit stubs
  /// </summary>
  public IReadOnlyDictionary<string, CommitStub> StubMap => _stubMap;

  /// <summary>
  /// If this graph contains the <see cref="Commit"/> with the given <paramref name="sha"/> id
  /// then return it. Otherwise return null (the stub is missing, or the stub is not yet connected)
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  public Commit? FindCommit(string sha)
  {
    return _stubMap.TryGetValue(sha, out var commit) ? commit.Target : null;
  }
  
  /// <summary>
  /// True if this graph contains the commit with the given <paramref name="sha"/> id.
  /// That is: it contains a stub, and that stub actually contains the <see cref="Commit"/>.
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  public bool ContainsCommit(string sha)
  {
    return _stubMap.TryGetValue(sha, out var result) && result.Target != null;
  }

  /// <summary>
  /// True if this graph contains a stub for the commit with the given <paramref name="sha"/> id.
  /// If true, the actual commit may or may not be in this graph. If false the actual commit
  /// definitely is not in this graph.
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  public bool ContainsStub(string sha)
  {
    return _stubMap.ContainsKey(sha);
  }

  /// <summary>
  /// Return all commits connected to this graph where <paramref name="predicate"/> returns true,
  /// but it does not return true for any of the child commits that are connected to this graph.
  /// </summary>
  /// <remarks>
  /// Just to be clear: that includes any such commits that do not have any children connected to
  /// this graph at all. Or no children whatsoever.
  /// </remarks>
  /// <param name="predicate">
  /// The predicate that returns true for matching commits.
  /// </param>
  /// <returns></returns>
  public IEnumerable<Commit> ConditionalTips(Func<Commit, bool> predicate)
  {
    var candidates =
      _stubMap.Values
      .Where(stub => stub.Target != null && predicate(stub.Target!));
    foreach(var candidate in candidates)
    {
      if(!candidate.Children.Any(child => child.Target != null && predicate(child.Target!)))
      {
        yield return candidate.Target!;
      }
    }
  }

  /// <summary>
  /// Return all commits connected to this graph where <paramref name="predicate"/> returns true,
  /// but it does not return true for any of the child commits that are connected to this graph.
  /// </summary>
  /// <remarks>
  /// Just to be clear: that includes any such commits that do not have any children connected to
  /// this graph at all. Or no children whatsoever.
  /// </remarks>
  /// <param name="predicate">
  /// The predicate that returns true for matching commits.
  /// </param>
  /// <returns></returns>
  public IEnumerable<Commit> ConditionalRoots(Func<Commit, bool> predicate)
  {
    var candidates =
      _stubMap.Values
      .Where(stub => stub.Target != null && predicate(stub.Target!));
    foreach(var candidate in candidates)
    {
      if(!candidate.Parents.Any(parent => parent.Target != null && predicate(parent.Target!)))
      {
        yield return candidate.Target!;
      }
    }
  }

  /// <summary>
  /// Return all tips of the graph
  /// </summary>
  /// <returns></returns>
  public IEnumerable<Commit> AllTips()
  {
    var candidates =
      _stubMap.Values
      .Where(stub => stub.Target != null);
    foreach(var candidate in candidates)
    {
      if(!candidate.Children.Any(child => child.Target != null))
      {
        yield return candidate.Target!;
      }
    }
  }

  /// <summary>
  /// Return all roots of the graph
  /// </summary>
  /// <returns></returns>
  public IEnumerable<Commit> AllRoots()
  {
    var candidates =
      _stubMap.Values
      .Where(stub => stub.Target != null);
    foreach(var candidate in candidates)
    {
      if(!candidate.Parents.Any(parent => parent.Target != null))
      {
        yield return candidate.Target!;
      }
    }
  }

  /// <summary>
  /// Connect <paramref name="commit"/> to its stub, setting the stub's 
  /// <see cref="CommitStub.Target"/>, adding the stubs for the parents to the list
  /// of <see cref="CommitStub.Parents"/> and for each parent register this stub
  /// as child in <see cref="CommitStub.Children"/>.
  /// </summary>
  /// <param name="commit"></param>
  /// <returns></returns>
  public CommitStub Connect(Commit commit)
  {
    var stub = GetStub(commit.Sha);
    stub.Target = commit;
    foreach(var parentCommit in commit.Parents)
    {
      var parentStub = GetStub(parentCommit.Sha);
      stub.AddParent(parentStub);
      parentStub.AddChild(stub);
    }
    return stub;
  }

  /// <summary>
  /// Connect each of the <paramref name="commits"/> to this <see cref="CommitStubGraph"/>
  /// (using <see cref="Connect(Commit)"/>).
  /// </summary>
  /// <param name="commits"></param>
  public void ConnectAll(IEnumerable<Commit> commits)
  {
    foreach(var commit in commits)
    {
      Connect(commit);
    }
  }

  /// <summary>
  /// Return commits in the child direction for connected stubs with precisely one child,
  /// including <paramref name="stub"/> itself.
  /// </summary>
  /// <param name="stub">
  /// The stub to use as starting point. Passing null returns an empty sequence.
  /// </param>
  /// <returns></returns>
  public IEnumerable<CommitStub> ChildStubChain(CommitStub? stub)
  {
    if(stub==null)
    {
      yield break;
    }
    while(stub.Target != null && stub.Children.Count == 1)
    {
      yield return stub;
      stub = stub.Children.First();
    }
    if(stub != null && stub.Target != null && stub.Children.Count != 1)
    {
      yield return stub;
    }
  }

  /// <summary>
  /// Return commits in the child direction for connected stubs with precisely one child,
  /// including the commit indicated by <paramref name="sha"/> itself.
  /// </summary>
  /// <param name="sha">
  /// The full commit identifier for the starting commit. If not found, an empty
  /// sequence is returned.
  /// </param>
  /// <returns></returns>
  public IEnumerable<CommitStub> ChildStubChain(string sha)
  {
    var stub = _stubMap.TryGetValue(sha, out var child) ? child : null;
    return ChildStubChain(stub);
  }

  /// <summary>
  /// Return commits in the child direction for connected stubs with precisely one child,
  /// including <paramref name="stub"/> itself.
  /// </summary>
  /// <param name="stub">
  /// The stub to use as starting point. Passing null returns an empty sequence.
  /// </param>
  /// <returns></returns>
  public IEnumerable<Commit> ChildChain(CommitStub? stub)
  {
    return ChildStubChain(stub).Select(stub => stub.Target!);
  }

  /// <summary>
  /// Return commits in the child direction for connected stubs with precisely one child,
  /// including the commit indicated by <paramref name="sha"/> itself.
  /// </summary>
  /// <param name="sha">
  /// The full commit identifier for the starting commit. If not found, an empty
  /// sequence is returned.
  /// </param>
  /// <returns></returns>
  public IEnumerable<Commit> ChildChain(string sha)
  {
    var stub = _stubMap.TryGetValue(sha, out var child) ? child : null;
    return ChildChain(stub);
  }

  /// <summary>
  /// Return commits in the parent direction for connected stubs with precisely one parent,
  /// including <paramref name="stub"/> itself.
  /// </summary>
  /// <param name="stub">
  /// The stub to use as starting point. Passing null returns an empty sequence.
  /// </param>
  /// <returns></returns>
  public IEnumerable<CommitStub> ParentStubChain(CommitStub? stub)
  {
    if(stub==null)
    {
      yield break;
    }
    while(stub.Target != null && stub.Parents.Count == 1)
    {
      yield return stub;
      stub = stub.Parents.First();
    }
    if(stub != null && stub.Target != null && stub.Parents.Count != 1)
    {
      yield return stub;
    }
  }

  /// <summary>
  /// Return commits in the parent direction for connected stubs with precisely one parent,
  /// including the commit indicated by <paramref name="sha"/> itself.
  /// </summary>
  /// <param name="sha">
  /// The full commit identifier for the starting commit. If not found, an empty
  /// sequence is returned.
  /// </param>
  /// <returns></returns>
  public IEnumerable<CommitStub> ParentStubChain(string sha)
  {
    var stub = _stubMap.TryGetValue(sha, out var child) ? child : null;
    return ParentStubChain(stub);
  }

  /// <summary>
  /// Return commits in the parent direction for connected stubs with precisely one parent,
  /// including <paramref name="stub"/> itself.
  /// </summary>
  /// <param name="stub">
  /// The stub to use as starting point. Passing null returns an empty sequence.
  /// </param>
  /// <returns></returns>
  public IEnumerable<Commit> ParentChain(CommitStub? stub)
  {
    return ParentStubChain(stub).Select(stub => stub.Target!);
  }

  /// <summary>
  /// Return commits in the parent direction for connected stubs with precisely one parent,
  /// including the commit indicated by <paramref name="sha"/> itself.
  /// </summary>
  /// <param name="sha">
  /// The full commit identifier for the starting commit. If not found, an empty
  /// sequence is returned.
  /// </param>
  /// <returns></returns>
  public IEnumerable<Commit> ParentChain(string sha)
  {
    var stub = _stubMap.TryGetValue(sha, out var child) ? child : null;
    return ParentChain(stub);
  }

  /// <summary>
  /// Get a <see cref="CommitStub"/> by its id
  /// </summary>
  /// <param name="sha"></param>
  /// <returns></returns>
  internal CommitStub GetStub(string sha)
  {
    if(!_stubMap.TryGetValue(sha, out var stub))
    {
      stub = new CommitStub(sha);
      _stubMap.Add(sha, stub);
    }
    return stub;
  }
}
