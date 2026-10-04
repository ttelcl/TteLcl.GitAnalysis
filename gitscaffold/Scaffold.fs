module Scaffold

// Utilities related to scaffold refs

open System
open System.Text.RegularExpressions

open LibGit2Sharp

open TteLcl.GitModel
open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

// Scaffold group names must match this regex and not match __hexRegex
let private __scaffoldGroupRegex = new Regex(@"^[a-zA-Z0-9]+(-[a-zA-Z0-9]+)*$")
// Matches any string that is a valid prefix of a hexadecimal sequence (including odd-count ones!)
let private __hexRegex = new Regex(@"^[a-fA-F0-9]+$")

type RefCreation =
  | Existing of Reference
  | Created of Reference

let isValidScaffoldGroup (group:string) =
  __scaffoldGroupRegex.IsMatch(group) && not(__hexRegex.IsMatch(group))

/// Get a 10 character tag for a git object (usually a commit)
let shatag (o:GitObject) = o.Sha.Substring(0, 10)

/// Create an ungrouped scaffold for the commit
let createCommitScaffold (commit:Commit) =
  let repo = (commit :> IBelongToARepository).Repository
  let tag = commit |> shatag
  let refname = $"refs/scaffold/c/{tag}"
  let existing = repo.Refs[refname]
  if existing <> null then
    existing |> RefCreation.Existing
  else
    repo.Refs.Add(refname, commit.Id) :> Reference |> RefCreation.Created

/// Create a grouped scaffold for a commit
let createGroupedScaffold group (commit:Commit) =
  if group |> isValidScaffoldGroup |> not then
    failwith $"'{group}' is not a valid group scaffold tag"
  let repo = (commit :> IBelongToARepository).Repository
  let tag = commit |> shatag
  let refname = $"refs/scaffold/g/{group}/{tag}"
  let existing = repo.Refs[refname]
  if existing <> null then
    existing |> RefCreation.Existing
  else
    repo.Refs.Add(refname, commit.Id) :> Reference |> RefCreation.Created

let tryResolveCommit (repo:Repository) (committish: string) =
  let o = repo.Lookup(committish)
  match o with
  | null ->
    None
  | :? Commit as commit ->
    commit |> Some
  | :? TagAnnotation as annotation ->
    match annotation.Target with
    | :? Commit as commit ->
      commit |> Some
    | _ ->
      // give up
      None
  | _ ->
    // unrecognized
    None



