module Journal.Tests.Parser

open Fuchu
open FParsec
open Journal.Parser.Combinators
open Journal.Parser.Types
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

[<Tests>]
let accountParsers = 
    testList "account parsers" [
        testList "subaccount" [
            testCase "may have any alphanumeric" <|
                fun _ ->
                    Assert.Equal(
                        "any alphanumeric",
                        Some("ABCabc123"),
                        parse subaccount "ABCabc123")

            testCase "may start with a digit" <|
                fun _ ->
                    Assert.Equal(
                        "may start with a digit",
                        Some("123abcABC"),
                        parse subaccount "123abcABC")
        ]

        testList "account" [
            testCase "multiple level account" <|
                fun _ ->
                    Assert.Equal(
                        "multiple level account",
                        Some(["Expenses"; "Food"; "Groceries"]),
                        parse account "Expenses:Food:Groceries")

            testCase "single level account" <|
                fun _ ->
                    Assert.Equal(
                        "single level account",
                        Some(["Expenses"]),
                        parse account "Expenses")
        ]
    ]

[<Tests>]
let quantityParser =
    testList "quantity" [
        testCase "negative quantity with no fractional part" <|
            fun _ -> Assert.Equal("quantity: -1,110", Some(-1110M), parse quantity "-1,110")

        testCase "positive quantity with no fractional part" <|
            fun _ -> Assert.Equal("quantity: 2314", Some(2314M), parse quantity "2314")

        testCase "negative quantity with fractional part" <|
            fun _ -> Assert.Equal("quantity: -1,110.38", Some(-1110.38M), parse quantity "-1,110.38")

        testCase "positive quantity with fractional part" <|
            fun _ -> Assert.Equal("quantity: 24521.793", Some(24521.793M), parse quantity "24521.793")
    ]

[<Tests>]
let symbolParser =
    testList "symbol" [
        testCase "quoted symbol \"MTF5004\"" <|
            fun _ ->
                Assert.Equal(
                    "symbol: \"MTF5004\"",
                    Some({Value = "MTF5004"; Quoted = true}),
                    parse symbol "\"MTF5004\"")

        testCase "unquoted symbol $" <|
            fun _ ->
                Assert.Equal(
                    "symbol: $",
                    Some({Value = "$"; Quoted = false}),
                    parse symbol "$")

        testCase "unquoted symbol US$" <|
            fun _ ->
                Assert.Equal(
                    "symbol: US$",
                    Some({Value = "US$"; Quoted = false}),
                    parse symbol "US$")
    ]

[<Tests>]
let amountParsers =
    testList "amountParsers" [
        testList "amount" [
            testCase "symbol then quantity with whitespace" <|
                fun _ ->
                    Assert.Equal(
                        "amount: $ 13,245.00",
                        Some({Value = 13245.00M; Symbol = {Value = "$"; Quoted = false}; Format = SymbolLeftWithSpace}),
                        parse amount "$ 13,245.00")

            testCase "symbol then quantity no whitespace" <|
                fun _ ->
                    Assert.Equal(
                        "amount: $13,245.00",
                        Some({Value = 13245.00M; Symbol = {Value = "$"; Quoted = false}; Format = SymbolLeftNoSpace}),
                        parse amount "$13,245.00")

            testCase "quantity then symbol with whitespace" <|
                fun _ ->
                    Assert.Equal(
                        "amount: 13,245.463 AAPL",
                        Some({Value = 13245.463M; Symbol = {Value = "AAPL"; Quoted = false}; Format = SymbolRightWithSpace}),
                        parse amount "13,245.463 AAPL")

            testCase "quantity then symbol no whitespace" <|
                fun _ ->
                    Assert.Equal(
                        "amount: -13,245.463\"MUTF803\"",
                        Some({Value = -13245.463M; Symbol = {Value = "MUTF803"; Quoted = true}; Format = SymbolRightNoSpace}),
                        parse amount "-13,245.463\"MUTF803\"")
        ]

        testList "amountOrInferred" [
            testCase "has amount" <|
                fun _ ->
                    Assert.Equal(
                        "amountOrInferred: $13,245.00",
                        Some(Provided, Some {Value = 13245.00M; Symbol = {Value = "$"; Quoted = false}; Format = SymbolLeftNoSpace}),
                        parse amountOrInferred "$13,245.00")

            testCase "inferred amount" <|
                fun _ ->
                    Assert.Equal(
                        "amountOrInferred: <empty>",
                        Some(Inferred, None),
                        parse amountOrInferred "")
        ]
    ]

[<Tests>]
let postingParser =
    testList "posting" [
        testCase "with all components" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Savings  $45.00  ;comment",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Savings";
                        Amount = Some {Value=45.00M; Symbol={Value="$"; Quoted=false}; Format=SymbolLeftNoSpace};
                        AmountSource = Provided;
                        Comment = Some "comment"
                    }),
                    parse posting "Assets:Savings  $45.00  ;comment")

        testCase "with all components (commodity)" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Investments  13.508 \"MUTF514\"  ;comment",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Investments";
                        Amount = Some {Value=13.508M; Symbol={Value="MUTF514"; Quoted=true}; Format=SymbolRightWithSpace};
                        AmountSource = Provided;
                        Comment = Some "comment"
                    }),
                    parse posting "Assets:Investments  13.508 \"MUTF514\"  ;comment")

        testCase "posting with whitespace but no comment" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Savings  $45.00  ",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Savings";
                        Amount = Some {Value=45.00M; Symbol={Value="$"; Quoted=false}; Format=SymbolLeftNoSpace};
                        AmountSource = Provided;
                        Comment = None
                    }),
                    parse posting "Assets:Savings  $45.00  ")

        testCase "posting with no whitespace or comment" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Savings  $45.00",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Savings";
                        Amount = Some {Value=45.00M; Symbol={Value="$"; Quoted=false}; Format=SymbolLeftNoSpace};
                        AmountSource = Provided;
                        Comment = None
                    }),
                    parse posting "Assets:Savings  $45.00")

        testCase "posting with inferred amount and comment" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Savings  ;comment",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Savings";
                        Amount = None;
                        AmountSource = Inferred;
                        Comment = Some "comment"
                    }),
                    parse posting "Assets:Savings  ;comment")

        testCase "posting with inferred amount, whitespace, and no comment" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Savings  ",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Savings";
                        Amount = None;
                        AmountSource = Inferred;
                        Comment = None
                    }),
                    parse posting "Assets:Savings  ")

        testCase "posting with inferred amount, no whitespace, no comment" <|
            fun _ ->
                Assert.Equal(
                    "posting: Assets:Savings",
                    Some(PostingLine {
                        LineNumber = 1L;
                        Account = "Assets:Savings";
                        Amount = None;
                        AmountSource = Inferred;
                        Comment = None
                    }),
                    parse posting "Assets:Savings")
    ]