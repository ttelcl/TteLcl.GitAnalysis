module AppFoo

open System

open ColorPrint
open CommonTools

(*
This is just an example 'subcommand'. Replace this file with your own functionality.
Or remove it completely if your application isn't structured as a collection of
subcommands.

This example shows off some coding patterns that I find useful
*)

// A type for holding this command's command line options
type private Options = {
  Foo: int // an integer value
  Bars: string list // zero or more strings
}

let private parseArgs args =
  // A recursive argument parser, peeling off one argument each time.
  // Takes an Options and the list of remaining arguments and returns an
  // "Options option". If None, that indicates an argument parsing error
  let rec parseMore o args =
    match args with
    | "-v" :: rest ->
      verbose <- true
      rest |> parseMore o
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-foo" :: footext :: rest ->
      let ok, foo = footext |> Int32.TryParse
      if ok then
        if foo > 0 then
          rest |> parseMore {o with Foo = foo}
        else
          cp $"\foExpecting a number above 0 but got \f0'\fb{foo}\f0'"
          None
      else
        cp $"\foExpecting a number after \fg-foo\fo but got \f0'{footext}\f0'"
        None
    | "-bar" :: bar :: rest ->
      rest |> parseMore {o with Bars = bar :: o.Bars}
    | [] ->
      // The recursion terminator. You probably want to reverse any lists in the Options
      // argument. Also a great place for last minute validation
      if o.Bars |> List.isEmpty then
        cp "\foNo \fg-bar\fos specified\f0."
        None
      else
        {o with Bars = o.Bars |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    Foo = 0
    Bars = []
  }

// The actual command execution, taking the parsed Options as argument
let private runFoo o =
  cp "Executing the '\fgfoo\f0' command (\fotemplate demo, to be replaced by your real code\f0)"
  cp $"The value of foo is \fb{o.Foo}\f0."
  cp $"You specified \fb{o.Bars.Length}\f0 bars:"
  for bar in o.Bars do
    cp $"  '\fc{bar}\f0'"
  0

// The entry point of this subcommand. Return 0 on success, or 1 on failure.
// "args" is a list of strings
let run args =
  // This example subcommand demonstrates one approach for command line parsing
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    // Something was wrong with the arguments. Give a detailed help message for this command
    Usage.usage "foo"
    1
  | Some o ->
    o |> runFoo
