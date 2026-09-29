open System

open ColorPrint
open CommonTools

let rec run arglist =
  match arglist with
  | "-v" :: rest ->
    verbose <- true
    rest |> run
  | "--help" :: _
  | "-h" :: _ ->
    Usage.usage "*"
    0
  | [] ->
    Usage.usage ""
    0  // program return status code to the operating system; 0 == "OK"
  // Example subcommand 'foo'. Replace with your own and add additional subcommands to your liking:
  | "foo" :: rest ->
    rest |> AppFoo.run
  | x :: _ ->
    cp $"\frUnknown command:\f0 '\fy{x}\f0'"
    Usage.usage ""
    1

[<EntryPoint>]
let main args =
  try
    args |> Array.toList |> run
  with
  | ex ->
    ex |> ExceptionTool.fancyExceptionPrint verbose
    resetColor ()
    1



