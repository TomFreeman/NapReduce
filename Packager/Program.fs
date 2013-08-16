open System

open Ionic.Zip
open NunitAssemblyParser
open CommandLine
open Helpers

type Argument() = 
    let makeOption v =
        if v = null then
            None
        else
            Some(v)

    [<Option('a', "assembly", Required=true, HelpText="Input assembly containing Nunit tests.")>]
    member val Assembly = "" with get, set
    [<Option('c', "category", DefaultValue=null, HelpText="Optionally provide a category")>]
    member val Cat = "" with get, set
    [<OptionArray('f', "additional-files", DefaultValue=null, HelpText="Optionally provide extra files that can't be resolved as direct dependencies")>]
    member val Extras = Array.empty<string> with get, set
    [<Option('o', "output-file", Required=true, HelpText="Provide the name for an output file.")>]
    member val OutputFile = "" with get, set

    member this.AdditionalFiles 
        with get () = 
            if this.Extras = null && this.Extras.Length = 0 then
                None
            else
                Some(this.Extras)

    member this.Category 
        with get () = makeOption this.Cat

let run (args:Argument) =
    let methods, assemblies = parseMethods args.Assembly args.Category

    let zipFileName = args.OutputFile
    let zipFile = new ZipFile(zipFileName)

    // Grr... No methods to determine relative path on IO.Path
    let currentDir = IO.Directory.GetCurrentDirectory() + @"\"
    let additionalFiles = 
        match args.AdditionalFiles with
        | None -> assemblies
        | Some(files) -> files |> Seq.iter (fun file -> printf "Adding extra file %s\n" file)
                         files |> Array.append assemblies
        |> Seq.distinct
        |> Seq.toArray
        |> Array.map (fun path -> path.Replace(currentDir, "") )

    zipFile.AddFiles(additionalFiles, true, null)

    zipFile.Save()

[<EntryPoint>]
let main argv =
    let args = new Argument()

    let showHelp () =
        let help = CommandLine.Text.HelpText.AutoBuild(args)
        Console.Write (help.ToString())
        -1

    if not (Parser.Default.ParseArguments(argv, args)) then
        showHelp ()
    else
        try
            do run args
            0
        with
            | ex -> Console.WriteLine(ex.Message) 
                    -1