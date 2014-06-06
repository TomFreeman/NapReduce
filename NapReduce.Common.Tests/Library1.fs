module SerialiserTests

open System
open Xunit
open FsCheck
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open NapReduce.Common

let uri = gen {
    return new Uri("http://test.com/")
    }


type UriGenerator =
    static member Uri() =
        {new Arbitrary<Uri>() with 
            override x.Generator = uri }

let setup () = Arb.register<UriGenerator>() |> ignore

[<Fact>]
let JobQuickCheck () =
    setup ()
    let roundTrip (job:Job) =
        let job2 = job
        job = job2
    Check.Quick roundTrip

[<Fact>]
let JobSerialise () =
    setup ()
    let roundTrip job =
        let json = JsonConvert.SerializeObject job
        let job2 = JObject.Parse(json).ToObject<Job>()
        job = job2
    Check.Quick roundTrip