module AppCommits

open System
open System.IO
open System.Text

open LibGit2Sharp

open TteLcl.GitModel
open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type private Options = {
  Witness: string
  ListCount: int
  DoDump: bool
  DoTips: bool
  IncludeGlobs: string list
  ExcludeGlobs: string list
}

let private parseArgs args =
  let rec parseMore o args =
    match args with
    | "-v":: rest ->
      verbose <- true
      rest |> parseMore o
    | "--help" :: _ 
    | "-h" :: _ ->
      None
    | "-repo" :: witness :: rest ->
      rest |> parseMore {o with Witness = witness}
    | "-list" :: ntxt :: rest ->
      let ok, n = ntxt |> Int32.TryParse
      if ok && n >= 0 then
        rest |> parseMore {o with ListCount = n}
      else
        cp $"\fo-list\fr: cannot parse '{ntxt}' as non-negative number\f0."
        None
    | "-dump" :: rest ->
      rest |> parseMore {o with DoDump = true}
    | "-tips" :: rest ->
      rest |> parseMore {o with DoTips = true}
    | "-i" :: includeGlob :: rest ->
      rest |> parseMore {o with IncludeGlobs = includeGlob :: o.IncludeGlobs}
    | "-x" :: excludeGlob :: rest ->
      rest |> parseMore {o with ExcludeGlobs = excludeGlob :: o.ExcludeGlobs}
    | [] ->
      {o with IncludeGlobs = o.IncludeGlobs |> List.rev; ExcludeGlobs = o.ExcludeGlobs |> List.rev} |> Some
    | x :: _ ->
      cp $"\foUnknown option \fy{x}\f0."
      None
  args |> parseMore {
    Witness = Environment.CurrentDirectory
    ListCount = 0
    DoDump = false
    DoTips = true
    IncludeGlobs = []
    ExcludeGlobs = []
  }

type private TipTailCommit = {
  Sha: string
  IsTip: bool
  Stamp: DateTimeOffset
}

let private abbreviateReference (refname: string) =
  if refname.StartsWith("refs/heads/") then
    "b:" + refname.Substring(11)
  elif refname.StartsWith("refs/remotes/") then
    "r:" + refname.Substring(13)
  elif refname.StartsWith("refs/tags/") then
    "t:" + refname.Substring(10)
  else
    refname

let private runCommits o =
  use repo = new GitRepo(o.Witness)
  let filter = new CommitFilter();
  if o.IncludeGlobs |> List.isEmpty |> not then
    let includes =
      o.IncludeGlobs
      |> Seq.map (fun glob -> repo.Repo.Refs.FromGlob(glob))
      |> Seq.toArray
    filter.IncludeReachableFrom <- includes
  if o.ExcludeGlobs |> List.isEmpty |> not then
    let excludes =
      o.ExcludeGlobs
      |> Seq.map (fun glob -> repo.Repo.Refs.FromGlob(glob))
      |> Seq.toArray
    filter.ExcludeReachableFrom <- excludes
  let commitSequence = repo.Repo.Commits.QueryBy(filter)
  let commits = commitSequence |> Seq.toArray
  cp $"Found \fb{commits.Length}\f0 commits"

  let commitMap = commits |> CommitMap.FromCommits
  let commitReferenceMap = new CommitReferenceMap(repo.Repo.Refs)

  let tips = commitMap.TipIds()
  let tails = commitMap.TailIds()
  cp $"Found \fg{tips.Count}\f0 tips and \fo{tails.Count}\f0 tails"

  let toTtc isTip (sha:string) =
    let commit = repo.Repo.Lookup<Commit>(sha)
    {
      Sha = sha
      IsTip = isTip
      Stamp = commit.Committer.When
    }

  let tipsAndTails =
    [
      tips |> Seq.map (toTtc true)
      tails |> Seq.map (toTtc false)
    ]
    |> Seq.concat
    |> Seq.sortBy (fun tot -> tot.Stamp)
    |> Seq.toArray
  tipsAndTails |> Array.Reverse

  for tot in tipsAndTails do
    let stamp = tot.Stamp.ToString("yyyy-MM-dd HH:mm:ss K")
    if tot.IsTip then
      cpx $"+ \fg{tot.Sha.Substring(0,8)}\f0  {stamp} "
    else
      cpx $"- \fo{tot.Sha.Substring(0,8)}\f0  {stamp} "
    let refs = tot.Sha |> commitReferenceMap.ReferencesForCommit |> Seq.sort |> Seq.toArray
    for r in refs do
      let color, shortname =
        if r.StartsWith("refs/heads/") then
          "\fg", r.Substring(11)
        elif r.StartsWith("refs/remotes/") then
          "\fc", r.Substring(13)
        elif r.StartsWith("refs/tags/") then
          "\fy", ("#" + r.Substring(10))
        else
          "\fr", r
      cpx $" {color}{shortname}\f0"
    cp "."

  if o.DoTips then
    let fileName = repo.Label + ".tips-tails.csv"
    do
      use csv = fileName |> startFile
      csv.WriteLine("kind,commit,stamp,references")
      for tot in tipsAndTails do
        let stamp = tot.Stamp.ToString("yyyy-MM-dd HH:mm:ss K")
        let kind = if tot.IsTip then "tip" else "tail"
        let refs =
          tot.Sha
          |> commitReferenceMap.ReferencesForCommit
          |> Seq.sort
          |> Seq.map abbreviateReference
          |> Seq.toArray
        let references = String.Join(" ", refs)
        csv.WriteLine($"{kind},{tot.Sha.Substring(0,8)},{stamp},{references}")
    fileName |> finishFile

  0

let run args =
  let oo = args |> parseArgs
  match oo with
  | None ->
    cp ""
    Usage.usage "commits"
    1
  | Some o ->
    o |> runCommits

