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
  DoShow: bool
  DoEdges: bool
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
    | "-show" :: rest ->
      rest |> parseMore {o with DoShow = true}
    | "-edges" :: rest
    | "-edge" :: rest ->
      rest |> parseMore {o with DoEdges = true}
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
    DoTips = false
    DoShow = false
    DoEdges = false
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

let timeSeparation (t1:DateTimeOffset) (t2:DateTimeOffset) =
  if t1.Year <> t2.Year then
    "5(year)", "", t1.ToString("yyyy"), t2.ToString("yyyy")
  elif t1.Month <> t2.Month then
    "4(month)", t1.ToString("yyyy") + "-", t1.ToString("MM"), t2.ToString("MM")
  elif t1.Day <> t2.Day then
    "3(day)", t1.ToString("yyyy-MM") + "-", t1.ToString("dd"), t2.ToString("dd")
  elif t1.Hour <> t2.Hour then
    "2(hour)", t1.ToString("yyyy-MM-dd") + " ", t1.ToString("HH"), t2.ToString("HH")
  elif t1.Minute <> t2.Minute then
    "1(minute)", t1.ToString("yyyy-MM-dd HH") + ":", t1.ToString("mm"), t2.ToString("mm")
  else
    "0(instant)", t1.ToString("yyyy-MM-dd HH:mm") + ":", t1.ToString("ss"), t2.ToString("ss")

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

  if o.DoShow then
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

  if o.DoEdges then
    let fileName = repo.Label + ".edges.csv"
    let mutable edgecount = 0
    let mutable externcount = 0
    do
      let cmap = commitMap.Commits
      use csv = fileName |> startFile
      csv.WriteLine("child,parent,extern,childstamp,parentstamp,separation,time-edge")
      for child in cmap.Values do
        for parent in child.Parents do
          let external = cmap.ContainsKey(parent.Sha) |> not
          let childStamp = child.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          let parentStamp = parent.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          let childShort = child.Sha.Substring(0, 8)
          let parentShort = parent.Sha.Substring(0, 8)
          let separation, commontime, pretime, postime = timeSeparation parent.Committer.When child.Committer.When
          let edgetext = $"{commontime}[{pretime}+{postime}]"
          csv.WriteLine($"{childShort},{parentShort},{external},{childStamp},{parentStamp},{separation},{edgetext}")
          edgecount <- edgecount + 1
          if external then
            externcount <- externcount + 1
      ()
    fileName |> finishFile
    cp $"Found \fb{edgecount}\f0 inter-commit edges, of which \fc{externcount}\f0 are external"

  if o.DoDump then
    let graph = new CommitStubGraph(commits)
    let fileName = repo.Label + ".commits.csv"
    do
      use csv = fileName |> startFile
      csv.WriteLine("commit,stamp,authored,interns,externs,children,sha")
      for commit in commits do
        let ok, stub = commit.Sha |> graph.StubMap.TryGetValue
        if ok then
          let id = commit.Sha.Substring(0,8)
          let commitStamp = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          let authorStamp = commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss K")
          let internalParentStubs =
            stub.Parents
            |> Seq.where (fun cs -> cs.Connected)
            |> Seq.toArray
          let externalParentStubs =
            stub.Parents
            |> Seq.where (fun cs -> cs.Connected |> not)
            |> Seq.toArray
          // Note that children are always 'connected', no need to prepare anything
          csv.WriteLine($"{id},{commitStamp},{authorStamp},{internalParentStubs.Length},{externalParentStubs.Length},{stub.Children.Count},{commit.Sha}")
        else
          cp $"\foCommit \fr{commit.Sha}\fo not found in graph\f0."
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

