using System;
using System.IO;
using System.Text;

using Xunit;
using Xunit.Abstractions;

using LibGit2Sharp;

using TteLcl.GitModel;
using TteLcl.GitModel.Builder;

namespace UnitTests.GitModel;

public class BuilderTests
{
  private readonly ITestOutputHelper _sink;

  public BuilderTests(ITestOutputHelper sink)
  {
    _sink = sink;
  }

  [Fact]
  public void CanFindGitDbFolder()
  {
    // Assumption: this test is run in a folder that is in a git repository
    var witness = Environment.CurrentDirectory;
    var gitdbFolder = GitRepo.FindGitDbFolder(witness);
    Assert.NotNull(gitdbFolder);
    Assert.True(Directory.Exists(gitdbFolder));
    Assert.True(File.Exists(Path.Combine(gitdbFolder, "config")));
    _sink.WriteLine($"Git DB folder for '{witness}' is '{gitdbFolder}'.");
  }

  [Fact]
  public void CanDetectNonGitFolder()
  {
    var witness = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    Assert.True(Directory.Exists(witness));
    var gitdbFolder = GitRepo.FindGitDbFolder(witness);
    Assert.Null(gitdbFolder);
    _sink.WriteLine($"Confirmed that '{witness}' is not in any GIT repository");
  }

  [Fact]
  public void FindingGitDbIsIdempotent()
  {
    // Assumption: this test is run in a folder that is in a git repository
    var witness = Environment.CurrentDirectory;
    var gitdbFolder = GitRepo.FindGitDbFolder(witness);
    Assert.NotNull(gitdbFolder);
    Assert.True(Directory.Exists(gitdbFolder));
    Assert.True(File.Exists(Path.Combine(gitdbFolder, "config")));

    var gitDbFolder2 = GitRepo.FindGitDbFolder(gitdbFolder);
    Assert.Equal(gitdbFolder, gitDbFolder2);
  }

  [Fact]
  public void CanOpenRepo()
  {
    var witness = Environment.CurrentDirectory;
    using var repo = new GitRepo(witness);
    Assert.NotNull(repo.Repo);
    var repo2 = repo.Repo;
    _sink.WriteLine("Refs:");
    foreach(var r in repo2.Refs)
    {
      if(r is DirectReference dr)
      {
        _sink.WriteLine($" direct:  {dr.TargetIdentifier}  <-  {dr.CanonicalName}");
      }
      else if(r is SymbolicReference sr)
      {
        _sink.WriteLine($" symbol:  {sr.TargetIdentifier,40}  <-  {sr.CanonicalName}");
      }
    }
  }
}
