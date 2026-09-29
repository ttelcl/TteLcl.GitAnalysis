module ExceptionTool

(*
  Provides functionality for displaying exception messages in detailed
  or brief styles.
*)

open System
open System.Diagnostics
open System.IO

open ColorPrint
open CommonTools

let rec fancyExceptionPrint showTrace (ex:Exception) =
  try
    cp $"\fr{ex.GetType().FullName}\f0: \fo{ex.Message}\f0."
    if showTrace then
      let stub = "?"
      let trace = new StackTrace(ex, true)
      for frame in trace.GetFrames() do
        printf "  "
        let fnm =
          if frame.HasSource() then
            let fnm = frame.GetFileName()
            cpx $"\fb{Path.GetFileName(fnm),15}\f0:\fg{frame.GetFileLineNumber(),4}\f0"
            fnm
          else
            cpx $"\fb{stub,15}\f0:    "
            null
        if frame.HasMethod() then
          let method = frame.GetMethod()
          cpx $" \fy{method.Name}("
          let pinfs = method.GetParameters()
          if pinfs.Length>0 then
            cpx $"\fo[{pinfs.Length}]\fy)"
          else
            cpx ")"
          cpx $" \fw{method.ReflectedType.Name}\f0"
        else
          cpx "\fr(?)\f0"
        if fnm <> null then
          cpx $" \fk({Path.GetDirectoryName(fnm)})\f0"
        cp "."
      ()
    finally
      resetColor()
  if ex.InnerException <> null then
    cpx "\fc----> \f0"
    ex.InnerException |> fancyExceptionPrint showTrace


