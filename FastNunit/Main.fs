open System
open MapReduce
open MapReduceHelpers
open Nunit
open Microsoft.Hadoop.MapReduce

type arg = 
    {
        Assembly : string;
        Category : string option
    }

let parseArgs (arguments:string[]) =
    { 
        Assembly = arguments.[0];
        Category = if arguments.Length > 1 then
                        Some arguments.[1]
                   else
                        None  
    }

// DO EVERYTHING!
let testCluster = new Uri("http://localhost:8085/")

let run (args : arg) =

    let hd = Hadoop.Connect()

    // Clean up the input files, in case something terrible has happened and we have invalid input that's not getting deleted
    hd.StorageSystem.Delete("input/nunit")
    hd.StorageSystem.Delete("input/nunitAssemblies")

    // Store the test assembly in the hadoop fs
    let assemblyName = args.Assembly.Substring(args.Assembly.LastIndexOf(@"\") + 1)
    let remoteAssemblyPath = "input/nunitAssemblies/" + assemblyName
    hd.StorageSystem.CopyFromLocal(args.Assembly, remoteAssemblyPath)
    let realAssemblyPath = hd.StorageSystem.GetFullyQualifiedPath(remoteAssemblyPath)

    let tests = parseMethods args.Assembly args.Category
                |> Seq.map (fun name -> let test = new Test() 
                                        test.Assembly <- assemblyName
                                        test.Test <- name
                                        test |> Newtonsoft.Json.JsonConvert.SerializeObject )

    hd.StorageSystem.WriteAllLines("input/nunit/input.txt", tests)

    // Execute the job, including the dll
    let result = hd.MapReduceJob.ExecuteJob<NunitJob>([|realAssemblyPath|])

    // Pull the results, and parse them back into an nunit xml file
    hd.StorageSystem.LsFiles("output/nunit")
    |> Seq.filter (fun path -> not (path.EndsWith("Success", System.StringComparison.OrdinalIgnoreCase)))
    |> (fun filteredList -> if (Seq.length filteredList) = 1 then
                                let text = hd.StorageSystem.ReadAllText(Seq.head filteredList)
                                let result = text.Substring(text.IndexOf("\t") + 1) |> fromB64Encoded
                                IO.File.WriteAllText("result.xml", result)
                            else
                                failwith "Too many results files, reduction failed")    

// Generic entry point, just to wrap args parsing and exception logging
[<EntryPoint>]
let main argv = 
    
    let args = parseArgs argv

    try
        do run args
        0
    with
        | ex -> Console.WriteLine(ex.Message) 
                -1