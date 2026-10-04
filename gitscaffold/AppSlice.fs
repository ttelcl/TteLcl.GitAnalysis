module AppSlice

open System
open System.Globalization

open LibGit2Sharp

open TteLcl.GitModel.Builder

open ColorPrint
open CommonTools

type private SliceMethod =
  | ByCommit of string
  | ByDate of DateTimeOffset

type private ScaffoldGroupSource =
  | Group of string
  | FromDate

// A type for holding this command's command line options
type private Options = {
  RepoWitness: string
  Method: SliceMethod option
  Scaffold: ScaffoldGroupSource option
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
    | "-scaffold" :: "-auto" :: rest ->
      rest |> parseMore {o with Scaffold = ScaffoldGroupSource.FromDate |> Some}
    | "-scaffold" :: group :: rest ->
      if group |> Scaffold.isValidScaffoldGroup |> not then
        cp $"\fo'\fy{group}\fo' is not a valid scaffold group name\f0."
        None
      else
        rest |> parseMore {o with Scaffold = group |> ScaffoldGroupSource.Group |> Some}
    | "-commit" :: sha :: rest ->
      // validate later
      rest |> parseMore {o with Method = sha |> SliceMethod.ByCommit |> Some}
    | "-before" :: dateText :: rest ->
      let ok, date =
        DateTimeOffset.TryParseExact(
          dateText, 
          [| "yyyy-MM-dd" |],
          CultureInfo.InvariantCulture,
          DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal)
      if ok then
        rest |> parseMore {o with Method = date |> SliceMethod.ByDate |> Some}
      else
        cp $"\foCannot parse '\fy{dateText}\fo' as a date. Expecting a \fcyyyy-MM-dd\fo format\f0."
        None
    | [] ->
      // The recursion terminator. You probably want to reverse any lists in the Options
      // argument. Also a great place for last minute validation
      if o.Method |> Option.isNone then
        cp "\foNo \fg-before\fo or \fg-commit\fo specified\f0."
        None
      elif o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
        cp $"\foInvalid or missing \fg-repo\fo: '\fy{o.RepoWitness}\fo' is not part of any GIT repository\f0." 
        None
      else
        o |> Some
    | x :: _ ->
      cp $"\foUnrecognized argument \f0'\fy{x}\f0'"
      None
  args |> parseMore {
    RepoWitness = Environment.CurrentDirectory
    Method = None
    Scaffold = None
  }

// The actual command execution, taking the parsed Options as argument
let private runSlice o =
  if o.RepoWitness |> GitRepo.FindGitDbFolder |> String.IsNullOrEmpty then
    cp $"\frError!\fo Not part of any GIT repository: \f0'\fy{o.RepoWitness}\f0'."
    1
  else
    use gitrepo = new GitRepo(o.RepoWitness)
    let repo = gitrepo.Repo
    cp $"Using repository \fg{gitrepo.Label}\f0 (\fc{gitrepo.GitDbFolder}\f0)"
    let refs = new ReferenceMap(gitrepo)
    let commitRefMap = new CommitReferenceMap(refs.References.Values)
    let commits =
      let filter = new CommitFilter();
      let includes =
        [ "refs/*" ] // for now: include everything and exclude nothing
        |> Seq.map (fun glob -> repo.Refs.FromGlob(glob))
        |> Seq.toArray
      filter.IncludeReachableFrom <- includes
      repo.Commits.QueryBy(filter)
      |> Seq.toArray
    cp $"Found \fb{commits.Length}\f0 commits in the repository."
    let graph = new CommitStubGraph(commits)
    let tipcount = graph.AllTips() |> Seq.length
    let rootcount = graph.AllRoots() |> Seq.length
    cp $"  (\fb{tipcount}\f0 tips and \fb{rootcount}\f0 roots)"
    let beforeOption =
      match o.Method with
      | None ->
        cp "\frInternal error\f0."
        None
      | Some(ByDate(date)) ->
        date |> Some
      | Some(ByCommit(sha)) ->
        let commit = repo.Lookup<Commit>(sha)
        if commit = null then
          cp $"\foCommit '{sha}\fo' not found\f0."
          None
        else
          // Add a tiny delay to get the "before-or-at" logic
          commit.Committer.When.AddMilliseconds(500.0) |> Some
    match beforeOption with
    | None ->
      1
    | Some(before) ->
      let beforeText = before.ToString("yyyy-MM-dd HH:mm:ss.f K")
      cp $"Slicing repository before \fc{beforeText}\f0."
      let beforeCount =
        commits
        |> Seq.where (fun c -> c.Committer.When < before)
        |> Seq.length
      cp $"Total commits before: \fb{beforeCount}\f0. Commits after: \fc{commits.Length - beforeCount}\f0."
      let tipsBefore =
        graph.ConditionalTips(fun c -> c.Committer.When < before)
        |> Seq.sortByDescending (fun c -> c.Committer.When)
        |> Seq.toArray
      let rootsBefore =
        graph.ConditionalRoots(fun c -> c.Committer.When < before)
        |> Seq.sortByDescending (fun c -> c.Committer.When)
        |> Seq.toArray
      cp $"Tips before: \fb{tipsBefore.Length}\f0. Roots before: \fc{rootsBefore.Length}\f0."
      if tipsBefore.Length < 1 then
        cp "\foThe repository did not exist at that time - there is nothing to slice. \frAborting\f0."
        1
      else
        cp "Slice edge commits:"
        for tip in tipsBefore do
          let stamp = tip.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          cp $"  tip  \fc{stamp} \fg{tip.Sha}\f0."
        for root in rootsBefore do
          let stamp = root.Committer.When.ToString("yyyy-MM-dd HH:mm:ss K")
          cp $"  root \fb{stamp} \fy{root.Sha}\f0."
        let scaffoldGroup =
          match o.Scaffold with
          | None -> None
          | Some(ScaffoldGroupSource.Group(group)) ->
            group |> Some
          | Some(ScaffoldGroupSource.FromDate) ->
            before.ToUniversalTime().ToString("yyyy-MM-dd") |> Some
        match scaffoldGroup with
        | Some(group) ->
          cp $"Creating or updating scaffold references in group '\fo{group}\f0':"
          for tip in tipsBefore do
            let dr = tip |> Scaffold.createGroupedScaffold group
            cp $" \fmscaffolding\f0 reference to commit \fc{tip.Sha}\f0 : \fg{dr.CanonicalName}\f0."
        | None -> ()
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
    Usage.usage "slice"
    1
  | Some o ->
    o |> runSlice
