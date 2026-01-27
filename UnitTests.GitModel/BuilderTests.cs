using System;
using System.Text;

using Xunit;
using Xunit.Abstractions;

using TteLcl.GitModel;
using TteLcl.GitModel.Builder;
using System.IO;

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
}
