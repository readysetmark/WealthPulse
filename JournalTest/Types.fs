module Journal.Tests.Types

open Fuchu
open Journal.Types

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
