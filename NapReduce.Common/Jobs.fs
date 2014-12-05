namespace NapReduce.Common

open System

type Test = {
    Name : string
    JobId : Guid
    }

type Result =
    | Passed of Test
    | Failed of Test * string[]

type Tests =
    | Category of string
    | Names of Test[]

type JobType =
        | Nunit
        | CodedUI

type Job = {
        Type        :JobType
        PackagePath :Uri
        Tests       :Tests
    }