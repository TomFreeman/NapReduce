open System
open System.Reflection
open Nunit
open NUnit.Framework
open Parallel
open System.Xml.Linq

let parseMethods assembly category =
    let assem = System.Reflection.Assembly.LoadFrom(assembly)

    let methods = assem.GetModules()
                  |> Seq.map (fun m -> m.GetTypes())
                  |> Seq.concat
                  |> Seq.map (fun t -> t.GetMethods(BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.Public ||| BindingFlags.DeclaredOnly))
                  |> Seq.concat
                  |> Seq.filter (fun m -> not (m.GetCustomAttribute<TestAttribute>(true) = null))

    match category with
    | Some(catFilter) -> methods |> Seq.filter (fun m -> let cats = m.GetCustomAttributes<CategoryAttribute>(true)
                                                         if cats = null then
                                                             false
                                                         else
                                                             cats |> Seq.exists (fun cat -> cat.Name = catFilter ))
    | None -> methods

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

let run args =
    // Discover all the test methods in the assembly
    let methods = parseMethods args.Assembly args.Category |> Seq.toArray

    // the intance of the master queue we will interact with
    use masterQueue = new MasterQueueAgent<MethodInfo, XDocument option>(3, mapNunit, (fun m -> runNunit args.Assembly (m.ReflectedType.FullName + "." + m.Name)))

    methods |> Seq.iter (fun m -> masterQueue.Post(m))

    masterQueue.Wait()

[<EntryPoint>]
let main argv = 
    
    let args = parseArgs argv

    try
        do run args
        0
    with
        | _ -> -1