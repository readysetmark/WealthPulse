module Journal.Tests.Parser

open Fuchu

open FParsec

open Journal.Parser.Combinators

[<Tests>]
let combinators =
    let testParse parser text =
        match run parser text with
        | Success(result, _, _) -> Some(result)
        | Failure(_, _, _)      -> None

    testList "combinators" [
        testList "skipWS" [
            testCase "skipWS should be ok when input is an empty string" <|
                fun _ -> Assert.Equal("skipWS \"\"", Some(()), testParse skipWS "")

            testCase "skipWS should be ok when input has no whitespace" <|
                fun _ -> Assert.Equal("skipWS \"alpha\"", Some(()), testParse skipWS "alpha")

            testCase "skipWS should skip a single space" <|
                fun _ -> Assert.Equal("skipWS \" \"", Some(()), testParse skipWS " ")

            testCase "skipWS should skip many spaces" <|
                fun _ -> Assert.Equal("skipWS \"     \"", Some(()), testParse skipWS "     ")

            testCase "skipWS should skip a single tab" <|
                fun _ -> Assert.Equal("skipWS \"\\t\"", Some(()), testParse skipWS "\t")

            testCase "skipWS should skip many tabs" <|
                fun _ -> Assert.Equal("skipWS \"\\t\\t\\t\"", Some(()), testParse skipWS "\t\t\t")

            testCase "skipWS should skip tabs and spaces" <|
                fun _ -> Assert.Equal("skipWS \"   \\t  \\t \\t", Some(()), testParse skipWS "   \t  \t \t")
        ]
        
        testList "parseDate" [
            testCase "parse date with / separator" <|
                fun _ -> Assert.Equal("parseDate \"2014/12/14\"", Some(new System.DateTime(2014, 12, 14)), testParse parseDate "2014/12/14")

            testCase "parse date with - separator" <|
                fun _ -> Assert.Equal("parseDate \"2014-12-14\"", Some(new System.DateTime(2014, 12, 14)), testParse parseDate "2014-12-14")

            testCase "invalid date separator" <|
                fun _ -> Assert.Raise("parseDate \"2014.12.14\"", typeof<System.FormatException>, fun _ -> ignore <| testParse parseDate "2014.12.14")
        ]
        
    ]