using System;
using System.Text;

using Xunit;
using Xunit.Abstractions;

using TteLcl.GitModel;


namespace UnitTests.GitModel;

public class GitHashTests
{
  private readonly ITestOutputHelper _sink;

  public GitHashTests(ITestOutputHelper sink)
  {
    _sink = sink;
  }

  [Fact]
  public void GitIdCacheInitializesWithJustTheZeroId()
  {
    var cache = new GitIdCache();
    Assert.NotNull(cache);
    var id0 = new ShortId(0L);
    var found = cache.TryGetValue(id0, out var gitid0);
    Assert.True(found);
    Assert.NotNull(gitid0);
    DumpGitId("zero", gitid0);
    Assert.Single(cache.Entries);
  }

  [Fact]
  public void CanGenerateBlobId()
  {
    var cache = new GitIdCache();
    Assert.NotNull(cache);
    var testBlobText = "test content\n";
    var testBlobBytes = Encoding.UTF8.GetBytes(testBlobText);
    var testGitId = cache.ForBlob(testBlobBytes);
    Assert.NotNull(testGitId);
    DumpGitId("'test content' plus newline", testGitId);
    Assert.Equal(2, cache.Entries.Count);
    // As found in https://git-scm.com/book/en/v2/Git-Internals-Git-Objects
    var expectedSha1 = "d670460b4b4aece5915caf5c68d12f560a9fe3e4";
    Assert.Equal(expectedSha1, testGitId.FullString);
    var expectedKey = 0xd670460b4b4aece5UL;
    Assert.Equal(expectedKey, testGitId.Id.Key);
    Assert.Equal(expectedKey, testGitId.Id); // implicit cast test
  }

  [Fact]
  public void CanReproduceBlob()
  {
    var cache = new GitIdCache();
    // As found in https://git-scm.com/book/en/v2/Git-Internals-Git-Objects
    var expectedSha = "d670460b4b4aece5915caf5c68d12f560a9fe3e4";
    var text = "test content\n";
    var gitId = cache.ForBlob(text);
    Assert.NotNull(gitId);
    DumpGitId("'test content' plus newline", gitId);
    Assert.Equal(expectedSha, gitId.FullString);
  }

  [Fact]
  public void CanReproduceTree()
  {
    var cache = new GitIdCache();

    // As found in a real-world repo, containing just a .gitignore file
    var expectedSha = "54c5e4a0017f00d5a94aeafbcb5413bf526d1771";
    // Beware! The output of git cat-file -p is misleading! Use "git cat-file tree"
    // instead and redirect it to a file (because it is binary)
    // WRONG! "100644 blob 044a93858877fc44672001c6424c62770bed97cd\t.gitignore\n"
    // Let's reconstruct the binary blob
    // (ref: https://stackoverflow.com/questions/14790681/what-is-the-internal-format-of-a-git-tree-object)
    Span<byte> testblob = stackalloc byte[38]; // we know the test case is 38 bytes.
    var prefixText = "100644 .gitignore\0";
    var prefixBytes = Encoding.UTF8.GetBytes(prefixText);
    prefixBytes.CopyTo(testblob[0..18]);
    var helperId = GitId.FromString("044a93858877fc44672001c6424c62770bed97cd");
    helperId.CopyTo(testblob[18..]);
    var gitId = cache.ForContent("tree", testblob);
    Assert.NotNull(gitId);
    DumpGitId("small tree sample", gitId);
    Assert.Equal(expectedSha, gitId.FullString);
  }

  private void DumpGitId(string heading, GitId gitId)
  {
    _sink.WriteLine($"GitId {heading}:");
    _sink.WriteLine($"    short-id={gitId.Id};  sha1={gitId.FullString}");
  }
}
