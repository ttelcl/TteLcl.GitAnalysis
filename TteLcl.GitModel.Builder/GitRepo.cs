using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

using TteLcl.GitModel;

namespace TteLcl.GitModel.Builder;

/// <summary>
/// Reference to a GIT repository
/// </summary>
public class GitRepo: IDisposable
{
  private bool _disposed;

  /// <summary>
  /// Create a new <see cref="GitRepo"/> instance referencing an existing git repository
  /// </summary>
  /// <param name="witnessPath">
  /// Any file or folder in an existing normal or bare GIT repository.
  /// </param>
  public GitRepo(
    string witnessPath)
  {
    IdCache = new GitIdCache();
    PathCache = new GitPathCache(IdCache);
    var dbFolder = FindGitDbFolder(witnessPath);
    if(String.IsNullOrEmpty(dbFolder))
    {
      throw new ArgumentException(
        $"Expecting the given path to be a file or folder in an existing GIT repository: {witnessPath}",
        nameof(witnessPath));
    }
    GitDbFolder = dbFolder;
    Repo = new Repository(dbFolder);
  }

  /// <summary>
  /// The Git ID cache
  /// </summary>
  public GitIdCache IdCache { get; }

  /// <summary>
  /// The git path cache
  /// </summary>
  public GitPathCache PathCache { get; }

  /// <summary>
  /// The folder containing the git data. For a normal repository that is the ".git" subfolder
  /// in the repository root. For a bare repository that is the repository folder itself.
  /// </summary>
  public string GitDbFolder { get; }

  /// <summary>
  /// The underlying (libgit2) repository
  /// </summary>
  public Repository Repo { get; }

  /// <summary>
  /// Locate the GIT database folder that contains (or is) the given folder or file
  /// <paramref name="witness"/>.
  /// </summary>
  /// <param name="witness">
  /// Any file or folder within the git repository to locate.
  /// </param>
  /// <returns></returns>
  /// <exception cref="InvalidOperationException"></exception>
  public static string? FindGitDbFolder(string witness)
  {
    if(!Path.IsPathRooted(witness))
    {
      witness = Path.GetFullPath(witness);
    }
    witness = witness.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    if(!Directory.Exists(witness))
    {
      if(File.Exists(witness))
      {
        var witness2 = Path.GetDirectoryName(witness);
        if(String.IsNullOrEmpty(witness2))
        {
          return null;
        }
        witness = witness2;
      }
      else
      {
        throw new InvalidOperationException(
          "Expecting an existing file or folder as argument");
      }
    }
    // witness now is a folder, as absolute path
    return FindGitDbFolderInternal(witness);
  }

  private static string? FindGitDbFolderInternal(string witnessFolder)
  {
    witnessFolder = witnessFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    if(String.IsNullOrEmpty(witnessFolder))
    {
      return null;
    }
    if(!Directory.Exists(witnessFolder))
    {
      return null;
    }
    // Assumption: witnessFolder is an existing absolute directory without trailing path separator
    if(IsGitDbFolder(witnessFolder))
    {
      return witnessFolder;
    }
    var gitChildFolder = Path.Combine(witnessFolder, ".git");
    if(Directory.Exists(gitChildFolder) && IsGitDbFolder(gitChildFolder))
    {
      return gitChildFolder;
    }
    var parent = Path.GetDirectoryName(witnessFolder);
    if(String.IsNullOrEmpty(parent))
    {
      return null;
    }
    return FindGitDbFolderInternal(parent);
  }

  private static bool IsGitDbFolder(string folder)
  {
    return
      folder.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
      && File.Exists(Path.Combine(folder, "config"));
  }

  /// <summary>
  /// Clean up
  /// </summary>
  public void Dispose()
  {
    if(!_disposed)
    {
      _disposed = true;
      Repo?.Dispose();
      GC.SuppressFinalize(this);
    }
  }
}
