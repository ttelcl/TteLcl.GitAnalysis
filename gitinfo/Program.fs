// (c) 2026  ttelcl / ttelcl

open System

open CommonTools
open ExceptionTool
open ColorPrint

let rec run arglist =
  // For subcommand based apps, split based on subcommand here
  match arglist with
  | "-v" :: rest ->
    verbose <- true
    rest |> run
  | "--help" :: _
  | "-h" :: _
  | [] ->
    Usage.usage ""
    0  // program return status code to the operating system; 0 == "OK"
  | "hash" :: rest ->
    rest |> AppHash.run
  | "stamp" :: rest ->
    rest |> AppStamp.run
  | "refs" :: rest 
  | "references" :: rest ->
    rest |> AppReferences.run
  | "commits" :: rest ->
    rest |> AppCommits.run
  | x :: _ ->
    cp $"\frUnknown command \f0'\fy{x}\f0'"
    1

[<EntryPoint>]
let main args =
  try
    args |> Array.toList |> run
  with
  | ex ->
    ex |> fancyExceptionPrint verbose
    resetColor ()
    1



