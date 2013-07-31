module FastNunit.Mapper.Tests

open MapReduce
open MapReduceHelpers
open FsUnit
open FsUnit.Xunit
open Xunit

open System.Xml.Linq

open Microsoft.Hadoop.MapReduce

let threePasses = [|"{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolated\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedTwo\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedThree\"}"|]

let threePassesOneInconclusive = [|"{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolated\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedTwo\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedThree\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ExampleTests.Inconclusive\"}"|]

let threePassesOneIgnored = [|"{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolated\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedTwo\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedThree\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ExampleTests.Ignored\"}"|]

let threePassesTwoFails = [|"{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolated\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedTwo\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolatedThree\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ExampleTests.WillFail\"}";
                    "{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ExampleTests.WillAlsoFail\"}"|]


let RunMultipleTests input =
    let output = StreamingUnit.Execute<NunitMapper, NunitReducer>(input)

    output.Result.Count |> should equal 1
    Seq.head output.Result
    |> decodeKeyValueResult

[<Fact>]
let ``Pointing at the wrong assembly path will result in no results``() =
    let result = RunMultipleTests [|"{\"Assembly\":\"FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolated\"}"|]

    let doc = XDocument.Parse(result)

    doc.Root.Attribute(XName.Get("total")).Value |> should equal "1"
    doc.Root.Attribute(XName.Get("failures")).Value |> should equal "1"

[<Fact>]
let ``Pointing at the right assembly path will result in tests being run``() =
    let testInput = [|"{\"Assembly\":\"..\\\\..\\\\..\\\\FastNunit.Tests\\\\bin\\\\Debug\\\\FastNunit.Tests.dll\",\"Test\":\"FastNunit.Tests.ParallelTests.AsyncTestsAreIsolated\"}"|]
    let output = StreamingUnit.Execute<NunitMapper, NunitReducer>(testInput)

    output |> should not' (be Null)

[<Fact>]
let ``After running tests, results are merged correctly.``() =
    let result = RunMultipleTests threePasses

    result.Length |> should be (greaterThan 0)

[<Fact>]
let ``merged test results are properly structured``() =
    let result = RunMultipleTests threePasses

    let doc = XDocument.Parse(result)

    doc.Root.Attribute(XName.Get("total")).Value |> should equal "3"

[<Fact>]
let ``failures are recorded correctly``() =
    let result = RunMultipleTests threePassesTwoFails

    let doc = XDocument.Parse(result)

    doc.Root.Attribute(XName.Get("total")).Value |> should equal "5"
    doc.Root.Attribute(XName.Get("failures")).Value |> should equal "2"

[<Fact>]
let ``Inconclusive tests are recorded correclty``() =
    let result = RunMultipleTests threePassesOneInconclusive

    let doc = XDocument.Parse(result)

    doc.Root.Attribute(XName.Get("total")).Value |> should equal "4"
    doc.Root.Attribute(XName.Get("inconclusive")).Value |> should equal "1"

[<Fact>]
let ``Ignored tests are recorded correclty``() =
    let result = RunMultipleTests threePassesOneIgnored

    let doc = XDocument.Parse(result)

    doc.Root.Attribute(XName.Get("total")).Value |> should equal "4"
    doc.Root.Attribute(XName.Get("not-run")).Value |> should equal "1"