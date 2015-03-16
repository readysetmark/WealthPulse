module Journal.Tests.Parser

open Fuchu
open FParsec
open Journal.Parser.Combinators
open Journal.Types


let parse parser text =
    match run parser text with
    | Success(result, _, _) -> Some(result)
    | Failure(_, _, _)      -> None


[<Tests>]
let whitespaceParsers =
    testList "skipWS" [
        testCase "skipWS should be ok when input is an empty string" <|
            fun _ -> Assert.Equal("skipWS: \"\"", Some(()), parse skipWS "")

        testCase "skipWS should be ok when input has no whitespace" <|
            fun _ -> Assert.Equal("skipWS: \"alpha\"", Some(()), parse skipWS "alpha")

        testCase "skipWS should skip a single space" <|
            fun _ -> Assert.Equal("skipWS: \" \"", Some(()), parse skipWS " ")

        testCase "skipWS should skip many spaces" <|
            fun _ -> Assert.Equal("skipWS: \"     \"", Some(()), parse skipWS "     ")

        testCase "skipWS should skip a single tab" <|
            fun _ -> Assert.Equal("skipWS: \"\\t\"", Some(()), parse skipWS "\t")

        testCase "skipWS should skip many tabs" <|
            fun _ -> Assert.Equal("skipWS: \"\\t\\t\\t\"", Some(()), parse skipWS "\t\t\t")

        testCase "skipWS should skip tabs and spaces" <|
            fun _ -> Assert.Equal("skipWS: \"   \\t  \\t \\t", Some(()), parse skipWS "   \t  \t \t")
    ]

[<Tests>]
let lineNumberParsers =
    testList "lineNumber" [
        testCase "first line is 1" <|
            fun _ -> Assert.Equal("lineNumber: 1", Some(1L), parse lineNumber "hi")
    ]

[<Tests>]
let commentParsers =
    testList "comment" [
        testCase "with leading space" <|
            fun _ -> Assert.Equal("comment: ; Hi", Some("Hi"), parse comment "; Hi")

        testCase "no leading space" <|
            fun _ -> Assert.Equal("comment: ;Hi", Some("Hi"), parse comment ";Hi")

        testCase "empty" <|
            fun _ -> Assert.Equal("comment: <empty>", Some(""), parse comment ";")
    ]

[<Tests>]
let dateParsers =
    testList "date parsers" [
        testList "year" [
            testCase "parse 4-digit year" <|
                fun _ -> Assert.Equal("year: 2015", Some(2015), parse year "2015")
        ]

        testList "month" [
            testCase "parse 2-digit month" <|
                fun _ -> Assert.Equal("month: 03", Some(3), parse month "03")
        ]

        testList "day" [
            testCase "parse 2-digit day" <|
                fun _ -> Assert.Equal("day: 15", Some(15), parse day "15")
        ]

        testList "date" [
            testCase "parse date with / separator" <|
                fun _ -> Assert.Equal("date: 2014/12/14", Some(new System.DateTime(2014, 12, 14)), parse date "2014/12/14")

            testCase "parse date with - separator" <|
                fun _ -> Assert.Equal("date: 2014-12-14", Some(new System.DateTime(2014, 12, 14)), parse date "2014-12-14")
        ]
    ]

[<Tests>]
let transactionHeaderSimpleFieldParsers =
    testList "transaction header simple field parsers" [
        testList "status" [
            testCase "cleared" <|
                fun _ -> Assert.Equal("status: *", Some(Cleared), parse status "*")

            testCase "uncleared" <|
                fun _ -> Assert.Equal("status: !", Some(Uncleared), parse status "!")
        ]

        testList "code" [
            testCase "long code" <|
                fun _ -> Assert.Equal("code: (conf# ABC-123-def)", Some("conf# ABC-123-def"), parse code "(conf# ABC-123-def)")

            testCase "short code" <|
                fun _ -> Assert.Equal("code: (89)", Some("89"), parse code "(89)")

            testCase "Empty code" <|
                fun _ -> Assert.Equal("code: ()", Some(""), parse code "()")
        ]

        testList "payee" [
            testCase "long payee" <|
                fun _ -> 
                    Assert.Equal(
                        "payee: WonderMart - groceries, toiletries, kitchen supplies",
                        Some ("WonderMart - groceries, toiletries, kitchen supplies"),
                        parse payee "WonderMart - groceries, toiletries, kitchen supplies")

            testCase "short payee" <|
                fun _ -> Assert.Equal("payee: W", Some("W"), parse payee "W")
        ]
    ]


[<Tests>]
let transactionHeaderParser =
    testList "transaction header" [
        testCase "with all fields" <|
            fun _ ->
                Assert.Equal(
                    "header with all fields",
                    Some({
                            LineNumber = 1L;
                            Date = new System.DateTime(2015,2,15);
                            Status = Cleared;
                            Code = Some("conf# abc-123");
                            Payee = "Payee";
                            Comment = Some("Comment")
                        }),
                    parse header "2015/02/15 * (conf# abc-123) Payee ;Comment")

        testCase "with code and no comment" <|
            fun _ ->
                Assert.Equal(
                    "header with code and no comment",
                    Some({
                            LineNumber = 1L;
                            Date = new System.DateTime(2015,2,15);
                            Status = Uncleared;
                            Code = Some("conf# abc-123");
                            Payee = "Payee";
                            Comment = None
                        }),
                    parse header "2015/02/15 ! (conf# abc-123) Payee")

        testCase "with comment and no code" <|
            fun _ ->
                Assert.Equal(
                    "header with comment and no code",
                    Some({
                            LineNumber = 1L;
                            Date = new System.DateTime(2015,2,15);
                            Status = Cleared;
                            Code = None;
                            Payee = "Payee";
                            Comment = Some("Comment")
                        }),
                    parse header "2015/02/15 * Payee ;Comment")

        testCase "with no code or comment" <|
            fun _ ->
                Assert.Equal(
                    "header with no code or comment",
                    Some({
                            LineNumber = 1L;
                            Date = new System.DateTime(2015,2,15);
                            Status = Cleared;
                            Code = None;
                            Payee = "Payee";
                            Comment = None
                        }),
                    parse header "2015/02/15 * Payee")
    ]