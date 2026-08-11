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
  public IEnumerable<Commit> ChildChain(CommitStub? stub)
  {
    if(stub==null)
    {
      yield break;
    }
    while(stub.Target != null && stub.Children.Count == 1)
    {
      yield return stub.Target;
      stub = stub.Children.First();
    }
    if(stub != null && stub.Target != null && stub.Children.Count != 1)
    {
      yield return stub.Target;
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
  public IEnumerable<Commit> ParentChain(CommitStub? stub)
  {
    if(stub==null)
    {
      yield break;
    }
    while(stub.Target != null && stub.Parents.Count == 1)
    {
      yield return stub.Target;
      stub = stub.Parents.First();
    }
    if(stub != null && stub.Target != null && stub.Parents.Count != 1)
    {
      yield return stub.Target;
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
