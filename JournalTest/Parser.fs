module Journal.Tests.Parser

open Fuchu
open FParsec
open Journal.Parser.Combinators


let testParse parser text =
    match run parser text with
    | Success(result, _, _) -> Some(result)
    | Failure(_, _, _)      -> None


[<Tests>]
let whitespaceParsers =
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

[<Tests>]
let dateParsers =
    testList "date parsers" [
        testList "year" [
            testCase "parse 4-digit year" <|
                fun _ -> Assert.Equal("year 2015", Some(2015), testParse year "2015")
        ]

        testList "month" [
            testCase "parse 2-digit month" <|
                fun _ -> Assert.Equal("month 03", Some(3), testParse month "03")
        ]

        testList "day" [
            testCase "parse 2-digit day" <|
                fun _ -> Assert.Equal("day 15", Some(15), testParse day "15")
        ]

        testList "date" [
            testCase "parse date with / separator" <|
                fun _ -> Assert.Equal("date 2014/12/14", Some(new System.DateTime(2014, 12, 14)), testParse date "2014/12/14")

            testCase "parse date with - separator" <|
                fun _ -> Assert.Equal("date 2014-12-14", Some(new System.DateTime(2014, 12, 14)), testParse date "2014-12-14")
        ]
    ]
