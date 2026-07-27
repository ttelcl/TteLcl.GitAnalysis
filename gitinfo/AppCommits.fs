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

type private CommitSide =
  | Tip
  | Intern
  | Tail


type private TipTailCommit = {
  Sha: string
  Side: CommitSide
  Stamp: DateTimeOffset
  Stamp2: DateTimeOffset
}

type private ClassifiedRef =
  | Branch of string
  | Remote of string
  | Tag of string
  | Other of string

type private ClassifiedRefs = {
  Branches: string list
  Remotes: string list
  Tags: string list
  Others: string list
}

let private foldRefs refs =
  let foldRef state r =
    match r with
    | Branch b -> {state with Branches = b :: state.Branches}
    | Remote r -> {state with Remotes = r :: state.Remotes}
    | Tag t -> {state with Tags = t :: state.Tags}
    | Other o -> {state with Others = o :: state.Others}
  let folded = refs |> Seq.fold foldRef {
      Branches = []
      Remotes = []
      Tags = []
      Others = []
    }
  {
    Branches = folded.Branches |> List.rev
    Remotes = folded.Remotes |> List.rev
    Tags = folded.Tags |> List.rev
    Others = folded.Others |> List.rev
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

let private classifyReference (refname: string) =
  if refname.StartsWith("refs/heads/") then
    refname.Substring(11) |> ClassifiedRef.Branch
  elif refname.StartsWith("refs/remotes/") then
    refname.Substring(13) |> ClassifiedRef.Remote
  elif refname.StartsWith("refs/tags/") then
    refname.Substring(10) |> ClassifiedRef.Tag
  else
    refname |> ClassifiedRef.Other

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
  let inners =
    commitMap.Commits.Keys
    |> Seq.where (fun sha -> sha |> tips.Contains |> not)
    |> Seq.toArray
  cp $"Found \fg{tips.Count}\f0 tips and \fo{tails.Count}\f0 tails and \fb{inners.Length}\f0 in-betweens"

  let commitSide commitId =
    if commitId |> tips.Contains then
      CommitSide.Tip
    elif commitId |> tails.Contains then
      CommitSide.Tail
    else
      CommitSide.Intern

  let toTtc sha =
    let side = sha |> commitSide
    let commit = repo.Repo.Lookup<Commit>(sha)
    {
      Sha = sha
      Side = side
      Stamp = commit.Committer.When
      Stamp2 = commit.Author.When
    }

  let relevantCommits =
    [
      tips |> Seq.map toTtc
      tails |> Seq.map toTtc
      inners |> Seq.map toTtc
    ]
    |> Seq.concat
    |> Seq.sortBy (fun tot -> tot.Stamp)
    |> Seq.toArray
  relevantCommits |> Array.Reverse

  for tot in relevantCommits do
    let stamp = tot.Stamp.ToString("yyyy-MM-dd HH:mm:ss K")
    let refs = tot.Sha |> commitReferenceMap.ReferencesForCommit |> Seq.sort |> Seq.toArray
    let isInner = tot.Side = CommitSide.Intern
    if (isInner |> not) || refs.Length > 0 then
      // Skip unlabeled internal nodes
      match tot.Side with
      | CommitSide.Tip ->
        cpx $"+ \fg{tot.Sha.Substring(0,8)}\f0  {stamp} "
      | CommitSide.Tail ->
        cpx $"- \fo{tot.Sha.Substring(0,8)}\f0  {stamp} "
      | CommitSide.Intern ->
        cpx $". \fk{tot.Sha.Substring(0,8)}\f0  \fk{stamp}\f0 "
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
        let color = if isInner then color.ToUpper() else color          
        cpx $" {color}{shortname}\f0"
      cp "."

  if o.DoTips then
    let fileName = repo.Label + ".tips-tails.csv"
    do
      use csv = fileName |> startFile
      csv.WriteLine("kind,commit,stamp,authored,branches,remotes,tags,others")
      for tot in relevantCommits do
        let stamp = tot.Stamp.ToString("yyyy-MM-dd HH:mm:ss K")
        let authored =
          if tot.Stamp = tot.Stamp2 then
            ""
          else
            tot.Stamp2.ToString("yyyy-MM-dd HH:mm:ss")
        let kind =
          match tot.Side with
          | CommitSide.Tip -> "tip"
          | CommitSide.Tail -> "tail"
          | CommitSide.Intern -> "inner"
        let refs =
          tot.Sha
          |> commitReferenceMap.ReferencesForCommit
          |> Seq.sort
          |> Seq.map classifyReference
          |> Seq.toArray
        if tot.Side <> CommitSide.Intern || refs.Length > 0 then
          // skip unlabeled internal nodes
          let foldedRefs = refs |> foldRefs
          let branches = String.Join(" ", foldedRefs.Branches)
          let remoteBranches = String.Join(" ", foldedRefs.Remotes)
          let tags = String.Join(" ", foldedRefs.Tags)
          let others = String.Join(" ", foldedRefs.Others)
          csv.WriteLine($"{kind},{tot.Sha.Substring(0,8)},{stamp},{authored},{branches},{remoteBranches},{tags},{others}")
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

