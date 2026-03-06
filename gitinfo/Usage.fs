// (c) 2026  ttelcl / ttelcl
module Usage

open CommonTools
open ColorPrint

let usage focus =
  cp "\fogitinfo \fyhash [\fg-t \fctype\f0] [\fg-fb \fcfile\f0|\fg-ft \fcfile\f0|\fg-s \fctext\f0|\fg-l \fcline\f0]"
  cp "   Calculate the git hash for a blob or other object."
  cp "  \fg-t \fctype\f0     The type of git object. Defaults to '\foblob\f0'."
  cp "  \fg-fb \fcfile\f0    The binary file whose content is the blob (or object)"
  cp "  \fg-ft \fcfile\f0    The text file whose content is the blob (or object) - after replacing CRLF pairs with LF"
  cp "  \fg-s \fctext\f0     The literal text content forming the blob content (as UTF8)"
  cp "  \fg-l \fcline\f0     Like \fg-s\f0, but with a LineFeed character (\fo\\n\f0) appended."
  cp ""
  cp "\fogitinfo \fystamp [\fg-repo \fcpath\f0] [\fg-ref \fcref\f0] [\fg-t \fcfile\f0] [\fg-a\f0]"
  cp "   Retrieve the time stamp of a git branch/tag/ref/commit and optionally touch a file with it"
  cp "   \fg-repo \fcpath\f0    Any path within the target repository. Defaults to the current directory"
  cp "   \fg-ref \fcref\f0      The commit (or reference, such as branch or tag) to get the commit stamp for"
  cp "   \fx\fx\fx              Defaults to the currently checked out commit (and has no default if there is none)"
  cp "   \fg-t \fcfile\f0       If given: create or touch the given file and set its stamp to the commit stamp"
  cp "   \fg-T \fcfile\f0       Like \fg-t\f0, but if the file already exists, ignore the commit stamp if it is older"
  cp "   \fg-a\f0\fx            If given: use the authored stamp instead of the committed stamp"
  cp ""
  cp "\fg-v               \f0Verbose mode"



