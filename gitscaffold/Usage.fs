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
    cp "  \fg-repo \fcwitness\f0  Use the repository that file or folder \fcwitness\f0 is in."
    cp "  \fg-bytime\f0\fx        Sort results by commit time"
    cp "  \fg-byname\f0\fx        Sort results by ref name"
    cp "  \fg-byhash\f0\fx        Sort results by commit hash"
    cp ""
  if showSection "" then
    if focus = "" then
      cp ""
    cp "\fyCommon options\f0"
    cp "  \fg-v\f0   Be more verbose. Also affects details of crash messages."
    cp "  \fg-h\f0   Show help message."



