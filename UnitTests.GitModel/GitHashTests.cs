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

  private void DumpGitId(string heading, GitId gitId)
  {
    _sink.WriteLine($"GitId {heading}:");
    _sink.WriteLine($"    short-id={gitId.Id};  sha1={gitId.FullString}");
  }
}
