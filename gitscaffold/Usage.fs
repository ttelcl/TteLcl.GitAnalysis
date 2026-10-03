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
    cp "\fogitscaffold \fylist\f0 \fg-repo \fcwitness\f0 [\fg-bytime\f0|\fg-byname\f0|\fg-byhash\f0]"
    cp "  List scaffold refs"
  if showDetail "list" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in."
    cp "  \fg-bytime\f0\fx          Sort results by commit time"
    cp "  \fg-byname\f0\fx          Sort results by ref name"
    cp "  \fg-byhash\f0\fx          Sort results by commit hash"
    cp ""
  if showSection "slice" then
    cpx "\fogitscaffold \fyslice\f0 [\fg-repo \fcwitness\f0] [\fg-commit \fcsha\f0|\fg-before \fcyyyy-MM-dd\f0]"
    cp " [\fg-scaffold \f0[\fcgroup\f0|\fo-auto\f0]]"
    cp "  Calculate the edge of the set of all commits before a given date, and optionally create scaffolds for those"
  if showDetail "slice" then
    cp "  \fg-repo \fcwitness\f0    Use the repository that file or folder \fcwitness\f0 is in. Default: current directory."
    cp "  \fg-before \fcdate\f0     Slice the commits made before midnight UTC on the given date (in ISO format, yyyy-MM-dd)"
    cp "  \fg-commit \fcsha\f0      Instead of slicing at a given date, slice at the time of the given commit."
    cp "  \fx\fx\fx                 That commit itself is included (so: 'before-or-at' instead of 'before')"
    cp "  \fg-scaffold \fcgroup\f0  Create scaffold refs for the commits at the edge, in the given \fcgroup\f0."
    cp "  \fx\fx\fx                 Without this option the operation is read-only."
    cp "  \fg-scaffold \fo-auto\f0  Likewise, but derive the group name from the slice date."
    cp ""
  if showSection "" then
    if focus = "" then
      cp ""
    cp "\fyCommon options\f0"
    cp "  \fg-v\f0   Be more verbose. Also affects details of crash messages."
    cp "  \fg-h\f0   Show help message."



