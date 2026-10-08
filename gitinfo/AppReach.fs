module AppReach

open System

open TteLcl.GitModel.Builder

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
  RepoWitness: string
  DoTips: bool
  DoRoots: bool
  DoDiff: bool
  Commits: string list
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
    | "-repo" :: repo :: rest ->
      rest |> parseMore {o with RepoWitness = repo}
    | [] ->
      // The recursion terminator. You probably want to reverse any lists in the Options
      // argument. Also a great place for last minute validation
      if (o.DoTips || o.DoRoots) |> not then
        cp "\foAt least one of \fg-tips\fo or \fg-roots\fo is required\f0."
        None
      elif o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
        cp $"\foInvalid or missing \fg-repo\fo: '\fy{o.RepoWitness}\fo' is not part of any GIT repository\f0." 
        None
      elif o.DoDiff && o.Commits.Length <> 2 then
        cp "\fo'\fg-diff\fo' requires exactly two '\fg-c\fo' options\f0."
        None
      else
        {o with Commits = o.Commits |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = Environment.CurrentDirectory
    DoTips = false
    DoRoots = false
    DoDiff = false
    Commits = []
  }

// The actual command execution, taking the parsed Options as argument
let private runApp o =
  use gitrepo = new GitRepo(o.RepoWitness)
  cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
  cp "\frNYI\f0."
  1

// The entry point of this subcommand. Return 0 on success, or 1 on failure.
// "args" is a list of strings
let run args =
  // This example subcommand demonstrates one approach for command line parsing
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    // Something was wrong with the arguments. Give a detailed help message for this command
    Usage.usage "reach"
    1
  | Some o ->
    o |> runApp
