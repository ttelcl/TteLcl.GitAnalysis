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
  cp "\fg-v               \f0Verbose mode"



