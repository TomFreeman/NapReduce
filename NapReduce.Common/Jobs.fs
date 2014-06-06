namespace NapReduce.Common

open System

type Result =
    | Passed
    | Failed of string[]

type Test = 
    | Name of string
    | Result of Result

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