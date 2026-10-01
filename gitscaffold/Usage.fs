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
  if showSection "foo" then
    // Summary of the 'foo' command including main arguments
    cp "\fogitscaffold \fyfoo\f0 [\fg-foo \fcnumber\f0] {\fg-bar \fctext\f0}"
    cp "  (Description of the 'foo' command)"
  if showDetail "foo" then
    // Details for the 'foo' command, especially argument descriptions
    cp "  \fg-foo \fcnumber\f0    Set the foo factor to \fcnumber\f0 (== Description of the '-foo' option)"
    cp "  \fg-bar \fctext\f0      Add a bar. Repeatable, required. (== Description of the '-bar' options)"
    // End the details section with an empty line:
    cp ""
  if showSection "baz" then
    cp "\fogitscaffold \fybaz\f0"
    cp "  (Description of the 'baz' command)"
  if showDetail "foo" then
    // End the details section with an empty line, even if there are no details
    cp ""
  if showSection "" then
    cp "\fyCommon options\f0"
    cp "  \fg-v\f0   Be more verbose. Also affects details of crash messages."
    cp "  \fg-h\f0   Show help message."



