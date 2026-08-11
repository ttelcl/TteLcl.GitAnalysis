using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LibGit2Sharp;

namespace TteLcl.GitModel.Builder;

/// <summary>
/// Extension methods
/// </summary>
public static class ExtensionMethods
{
  /// <summary>
  /// Try to resolve a <see cref="Reference"/> to a <see cref="Commit"/>
  /// (returning <see langword="null"/> on failure)
  /// </summary>
  /// <param name="r"></param>
  /// <returns></returns>
  public static Commit? TryResolveToCommit(this Reference r)
  {
    var dr = r.ResolveToDirectReference();
    var commit =
      dr.Target switch {
        Commit c1 => c1,
        TagAnnotation ta =>
          ta.Target switch {
            Commit c2 => c2,
            _ => null // give up; too complex to bother
          },
        _ => null // unrecognized
      };
    return commit;
  }

}
