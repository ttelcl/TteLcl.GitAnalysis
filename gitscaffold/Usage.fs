module Usage

open ColorPrint

/// <summary>
/// Show the usage message. The detail level is controlled by the focus parameter
/// </summary>
/// <param name="focus">
/// If an empty string: show an overview of the app and summaries of its subcommands.
/// If "*": also show details of all subcommands (triggered by passing '-h' on the commandline instead of a subcommand)
/// If a command name: Only show that subcommand's summary and details
/// </param>
let usage focus =
  let showSection section =
    focus = "" || focus = section || focus = "*"
  let showDetail section =
    focus = section || focus = "*"
  if showSection "" then
    cp "\foManage 'scaffold' refs in a git repository\f0."
    cp "   Scaffold refs are refs that are similar to tags, but are outside the 'usual' ref namespaces, so"
    cp "   are not used directly by git commands. These can help in keeping commits that have no"
    cp "   other reference to be kept alive. And they allow such commits to be used in git commands like"
    cp "   'bundle' that require refs, not commits. Scaffold refs start with '\fyrefs/scaffold/\f0'"
    cp ""
  if showSection "list" then
    cp "\fogitscaffold \fylist\f0 [\fg-repo \fcwitness\f0] [\fg-bytime\f0|\fg-byname\f0|\fg-byhash\f0]"
    cp "  List scaffold refs"
  if showDetail "list" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in. Default: current directory."
    cp "  \fg-bytime\f0\fx          Sort results by commit time"
    cp "  \fg-byname\f0\fx          Sort results by ref name"
    cp "  \fg-byhash\f0\fx          Sort results by commit hash"
    cp ""
  if showSection "slice" then
    cpx "\fogitscaffold \fyslice\f0 [\fg-repo \fcwitness\f0] [\fg-commit \fcid\f0|\fg-before \fcyyyy-MM-dd\f0]"
    cp " [\fg-scaffold \f0[\fcgroup\f0|\fo-auto\f0]]"
    cp "  Calculate the edge of the set of all commits before a given date, and optionally create scaffolds for those"
  if showDetail "slice" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in. Default: current directory."
    cp "  \fg-before \fcdate\f0     Slice the commits made before midnight UTC on the given date (in ISO format, yyyy-MM-dd)"
    cp "  \fg-commit \fcid\f0       Instead of slicing at a given date, slice at the time of the given commit."
    cp "  \fx\fx\fx                 '\fcid\f0' can be an (abbreviated) commit hash, branch, tag, or full ref"
    cp "  \fx\fx\fx                 That commit itself is included (so: 'before-or-at' instead of 'before')"
    cp "  \fg-scaffold \fcgroup\f0  Create scaffold refs for the commits at the edge, in the given \fcgroup\f0."
    cp "  \fx\fx\fx                 Without this option the operation is read-only."
    cp "  \fg-scaffold \fo-auto\f0  Likewise, but derive the group name from the slice date."
    cp ""
  if showSection "create" then
    cp "\fogitscaffold \fycreate\f0 [\fg-repo \fcwitness\f0] [\fg-g \fcgroup\f0] {\fg-c \fccommit\f0}"
    cp "  Create ungrouped or grouped scaffolds for the specified commits"
  if showDetail "create" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in."
    cp "  \fg-g \fcgroup\f0         If present: the group in which to create the scaffolds."
    cp "  \fx\fx\fx                 If not present: the scaffolds created will be of the 'ungrouped' kind"
    cp "  \fg-c \fccommit\f0        The commit to create a scaffold for. Repeatable. '\fccommit\f0' can be"
    cp "  \fx\fx\fx                 a commit hash, a branch, a tag, or a full ref path"
    cp ""
  if showSection "drop" then
    cpx "\fogitscaffold \fydrop\f0 [\fg-repo \fcwitness\f0] {\fg-c \fccommit\f0} [\fg-g \fcgroup\f0|\fg-all\f0]"
    cp " [\fg-dry\f0] [\fr-force\f0]"
    cp "  Drop ungrouped or grouped scaffolds for the specified commits. If dropping a scaffold would make"
    cp "  the commit unreachable, a grouped commit is converted to ungrouped, while an ungrouped one aborts."
  if showDetail "drop" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in."
    cp "  \fg-c \fccommit\f0        The commit to drop the scaffold for. Repeatable."
    cp "  \fg-g \fcgroup\f0         If present: the group from which to drop the scaffolds given by \fg-c\f0"
    cp "  \fx\fx\fx                 If not present: drop ungrouped scaffolds."
    cp "  \fg-all\f0\fx             Drop all scaffolds (all grouped and ungrouped) from the selected commits."
    cp "  \fg-dry\f0\fx             Do not actually drop anything, only report what would happen."
    cp "  \fr-force\f0\fx           Allows dropping ungrouped scaffolds that would cause their commit to become"
    cp "  \fx\fx\fx                 unreachable. If not present such cases cause the command to abort."
    cp "  \fx\fx\fx                 \foUnreachable commits are effectively permanently deleted from the repo\f0."
    cp ""
  if showSection "dropgroup" then
    cp "\fogitscaffold \fydropgroup\f0 [\fg-repo \fcwitness\f0] \fg-g \fcgroup\f0 [\fr-force\f0]"
    cp "  Drop all scaffolds in a group. Scaffolds for which dropping would cause a commit to become"
    cp "  unreachable are converted to an ungrouped scaffold instead of dropping, unless \fr-force\f0 is given."
  if showDetail "dropgroup" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in."
    cp "  \fg-g \fcgroup\f0         The group whose scaffolds are to be dropped."
    cp "  \fr-force\f0\fx           Allows dropping grouped scaffolds that would cause their commit to become"
    cp "  \fx\fx\fx                 unreachable. If not present such cases cause the scaffold to be converted"
    cp "  \fx\fx\fx                 to an ungrouped scaffold."
    cp "  \fx\fx\fx                 \foUnreachable commits are effectively permanently deleted from the repo\f0."
    cp ""
  if showSection "" then
    if focus = "" then
      cp ""
    cp "\fyCommon options\f0"
    cp "  \fg-v\f0   Be more verbose. Also affects details of crash messages."
    cp "  \fg-h\f0   Show help message."



