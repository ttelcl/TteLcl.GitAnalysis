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

let isValidScaffoldGroup (group:string) =
  __scaffoldGroupRegex.IsMatch(group) && not(__hexRegex.IsMatch(group))

