module Journal.Tests.Types

open Fuchu
open Journal.Types

[<Tests>]
let accountTests =
    testList "getAccountLineage" [
        testCase "empty account" <| fun _ ->
            let account = ""
            let accountLineage = [""]
            Assert.Equal("empty account", accountLineage, Account.getAccountLineage account)

        testCase "single level account" <| fun _ ->
            let account = "Level1"
            let accountLineage = ["Level1"]
            Assert.Equal("single level account", accountLineage, Account.getAccountLineage account)

        testCase "two level account" <| fun _ ->
            let account = "Level1:Level2"
            let accountLineage = ["Level1"; "Level1:Level2"]
            Assert.Equal("two level account", accountLineage, Account.getAccountLineage account)

        testCase "three level account" <| fun _ ->
            let account = "Level1:Level2:Level3"
            let accountLineage = ["Level1"; "Level1:Level2"; "Level1:Level2:Level3"]
            Assert.Equal("three level account", accountLineage, Account.getAccountLineage account)
    ]

[<Tests>]
let symbolConfigTests = 
    testList "render" [
        testCase "quoted symbol" <| fun _ ->
            let symbolConfig = {
                Symbol = { Value = "MUTF514"; Quoted = true };
                GoogleFinanceSearchSymbol = "MUTF_CA:MUTF514";
            }
            Assert.Equal("quoted symbol", "SC \"MUTF514\" MUTF_CA:MUTF514", SymbolConfig.render symbolConfig)

        testCase "unquoted symbol" <| fun _ ->
            let symbolConfig = {
                Symbol = { Value = "APPL"; Quoted = false };
                GoogleFinanceSearchSymbol = "MUTF_CA:APPL";
            }
            Assert.Equal("unquoted symbol", "SC APPL MUTF_CA:APPL", SymbolConfig.render symbolConfig)
    ]
