namespace WealthPulse

open FParsec
open Journal

// Parser module contains functions for parsing the Ledger journal file

module Parser =
    
    // AST module contains transient types for the Abstract Syntax Tree used by the parser
    // Eventually we convert all the data to the types in the Journal module

    module private AST =

        type CommodityValue =
            | TotalCost of Amount
            | UnitCost of Amount

        type ASTEntry = {
            Account: string;
            EntryType: EntryType;
            Amount: Amount option;
            CommodityValue: CommodityValue option;
            Comment: string option
        }

        type ASTNode =
            | Comment of string
            | Transaction of Header * ASTNode list
            | Entry of ASTEntry


    
    // Parse module contains FParsec parsing functions

    module private Parse =
        open AST
        
        /// Call Trim() on a string
        let trim (s : string) = 
            s.Trim()

        /// Skip whitespace as spaces and tabs
        let skipWS = 
            let isWS c = c = ' ' || c = '\t'
            skipManySatisfy isWS

        /// Parse a date
        let parseDate =
            let isDateChar c = isDigit c || c = '/' || c = '-' || c = '.'
            many1SatisfyL isDateChar "Expecting a date separated by / or - or ." .>> skipWS |>> System.DateTime.Parse

        /// Parse transaction status as Cleared or Uncleared
        let parseTransactionStatus = 
            let parseCleared = charReturn '*' Cleared
            let parseUncleared = charReturn '!' Uncleared
            (parseCleared <|> parseUncleared) .>> skipWS
    
        /// Parse a code between parentheses
        let parseCode = 
            let codeChar = noneOf ");\r\n"
            between (pstring "(") (pstring ")") (manyChars codeChar) .>> skipWS

        /// Parse a comment that begins with a semi-colon (;)
        let parseComment =
            let commentChar = noneOf "\r\n"
            pstring ";" >>. manyChars commentChar |>> trim

        /// Parse a description/payee
        let parsePayee = 
            let payeeChar = noneOf ";\r\n"
            many1Chars payeeChar |>> trim

        /// Parse an account
        let parseAccount =
            let extractAccountAndEntryType (account :string) =
                let entryType = 
                    match account.[0] with
                        | '(' -> VirtualUnbalanced
                        | '[' -> VirtualBalanced
                        | _ -> Balanced
                let account = (account.TrimStart [| '('; '[' |]).TrimEnd [| ')'; ']' |]
                (account, entryType)
            let accountChar = noneOf " ;\t\r\n" 
            let stringsSepEndBy p sep =
                Inline.SepBy(elementParser = p,
                             separatorParser = sep,
                             ?separatorMayEndSequence = Some true,
                             stateFromFirstElement = (fun str -> 
                                                        let sb = new System.Text.StringBuilder()
                                                        sb.Append(str : string)),
                             foldState = (fun sb sep str -> sb.Append(sep : string)
                                                              .Append(str : string)),
                             resultFromState = (fun sb -> sb.ToString()))
            stringsSepEndBy (many1Chars accountChar) (pstring " ") .>> skipWS |>> extractAccountAndEntryType

        /// Parse the numeric portion of an amount
        let parseAmountNumber =
            let isAmountChar c = isDigit c || c = '.' || c = ','
            attempt (pipe2 <| pchar '-' <| many1Satisfy isAmountChar <| (fun a b -> a.ToString() + b)) 
            <|> (many1Satisfy isAmountChar)
            .>> skipWS
            |>> System.Decimal.Parse

        /// Parse the symbol portion of an amount
        let parseSymbol =
            let quotedIdentifier = noneOf "\r\n\""
            let identifier = noneOf "-0123456789., @;\r\n\""
            attempt (pchar '\"' >>. manyCharsTill quotedIdentifier (pchar '\"'))
            <|> attempt (many1Chars identifier)
            .>> skipWS

        /// Parse an amount that includes the numerical value and the symbol.
        /// The symbol can come before or after the amount. If the symbol contains
        /// numbers or a space, it must be quoted.
        let parseAmount =
            let createAmount (amount, symbol) = {Amount = amount; Symbol = symbol}
            let amountTuple amount = (amount, None)
            let reverse (a,b) = (b,a)
            attempt (parseAmountNumber .>>. (parseSymbol |>> Some) |>> createAmount)
            <|> attempt (parseAmountNumber |>> amountTuple |>> createAmount)
            <|> ((parseSymbol |>> Some) .>>. parseAmountNumber |>> reverse |>> createAmount)

        /// Parse a commodity value amount in terms of UnitCost or TotalCost
        let parseCommodityValue =
            let parseTotalCost = (pstring "@@" .>> skipWS >>. parseAmount) |>> TotalCost
            let parseUnitCost = (pstring "@" .>> skipWS >>. parseAmount) |>> UnitCost
            attempt (parseTotalCost <|> parseUnitCost)

        /// Parse a complete transaction header
        let parseTransactionHeader =
            let createHeader date status code payee comment =
                {Date=date; Status=status; Code=code; Description=payee; Comment=comment}        
            pipe5 parseDate parseTransactionStatus (opt parseCode) parsePayee (opt parseComment) createHeader

        /// Parse a complete transaction entry
        let parseTransactionEntry =
            let createEntry (account, entryType) amount value comment =
                {Account=account; EntryType=entryType; Amount=amount; CommodityValue=value; Comment=comment}
            pipe4 parseAccount (opt parseAmount) (opt parseCommodityValue) (opt parseComment) createEntry |>> Entry

        /// Parse a journal comment line
        let parseCommentLine = parseComment |>> Comment

        /// Parse a complete transaction
        let parseTransaction =
            let parseEntry = attempt (skipWS >>. (parseTransactionEntry <|> parseCommentLine) .>> newline)
            parseTransactionHeader .>> newline
            .>>. many parseEntry
            |>> Transaction

        /// Parse a complete ledger journal
        let parseJournal =
            sepEndBy (parseCommentLine <|> parseTransaction) (many (skipWS >>. newline))


        
    // Module PostProcess contains post-parsing transformations

    module private PostProcess =
        open AST

        /// Transforms the AST data structure into a list of (Header, ASTEntry list) tuples
        /// Basically, we're dropping all the comment nodes
        let transformASTToTransactions ast =
            let transactionFilter (node: ASTNode) =
                match node with
                | Transaction(_,_) -> true
                | _ -> false

            let getTransactionHeader (node: ASTNode) =
                match node with
                | Transaction(header, ast) -> (header, ast)
                | _ -> failwith "Unexpected AST value in getTransactionHeader"

            let transactionEntryFilter (node: ASTNode) =
                match node with
                | Entry(_) -> true
                | _ -> false

            let getTransactionEntry (node: ASTNode) =
                match node with
                | Entry(te) -> te
                | _ -> failwith "Unexpected AST value in getTransactionEntry"

            let getTransactionEntries (header, ast) =
                let entries = 
                    ast
                    |> List.filter transactionEntryFilter
                    |> List.map getTransactionEntry
                (header, entries)

            ast
            |> List.filter transactionFilter
            |> List.map getTransactionHeader
            |> List.map getTransactionEntries


        /// Verifies that transactions balance for all entry types and autobalances
        /// transactions if one amount is missing.
        /// TODO: The possibility of different symbols for amounts is completely ignored right now.
        let balanceTransactions transactions =
            // Virtual Unbalanced transactions must have an amount (since they are unbalanced)
            let verifyVirtualUnbalanced entries =
                let virtualUnbalancedMissingAmount =
                    List.filter (fun entry -> entry.EntryType = VirtualUnbalanced && entry.Amount.IsNone) entries
                match List.length virtualUnbalancedMissingAmount with
                | 0 -> entries
                | otherwise -> failwith "Encountered virtual unbalanced entry missing an amount."

            // Generic balance checker for balanced entry types. Balanced entries should sum 0 or have only 1
            // amount missing (which can be auto-balanced).
            let verifyBalanced entryType entries =
                let balancedEntries = List.filter (fun entry -> entry.EntryType = entryType) entries
                let symbol =
                    balancedEntries
                    |> List.tryPick (fun entry -> if entry.Amount.IsSome && entry.Amount.Value.Symbol.IsSome 
                                                  then entry.Amount.Value.Symbol
                                                  else None)
                let sum =
                    balancedEntries
                    |> List.fold (fun sum entry -> if entry.Amount.IsSome
                                                   then sum + entry.Amount.Value.Amount
                                                   else sum)
                                 0M
                let numMissing =
                    balancedEntries
                    |> List.filter (fun entry -> entry.Amount.IsNone)
                    |> List.length
                (sum, symbol, numMissing)

            // Balanced entry types can be autobalanced as long as there is only 1 amount missing.
            let autobalance entryType entries =
                match verifyBalanced entryType entries with
                | _, _, numMissing when numMissing > 1 -> failwith "Encountered balanced transaction with more than one amount missing."
                | sum, symbol, numMissing when numMissing = 1 ->
                    entries
                    |> List.map (fun entry -> if entry.Amount.IsNone
                                              then { entry with Amount = Some {Amount = -sum; Symbol = symbol} }
                                              else entry)
                | sum, _, _ when sum <> 0M -> failwith "Encountered balanced transaction that is not balanced."
                | otherwise -> entries

            // Transform pipeline to balance transaction entries
            let balanceEntries =
               verifyVirtualUnbalanced
               >> autobalance VirtualBalanced
               >> autobalance Balanced

            transactions
            |> List.map (fun (header, entries) -> (header, balanceEntries entries))
            

        /// Convert to a list of journal entries (transaction entries)
        let toEntryList ts =
            let transactionToJournal (h, es) =
                let header = ({ Date=h.Date; Status=h.Status; Code=h.Code; Description=h.Description; Comment=h.Comment; } : Header)
                let toEntry e =
                    let getAccountLineage (account: string) =
                        /// Use with fold to get all combinations.
                        /// ex: if we have a:b:c, returns a list of a:b:c; a:b; a
                        let combinator (s: string list) (t: string) =
                            if not s.IsEmpty then (s.Head + ":" + t) :: s else t :: s
                        account.Split ':'
                        |> Array.fold combinator []
                        |> List.rev
                    let getValue (value: CommodityValue option) (amount : Amount) =
                        match value with
                        | Some(TotalCost(v)) -> Some v
                        | Some(UnitCost(v)) -> Some {Amount = v.Amount * amount.Amount; Symbol = v.Symbol}
                        | None -> None
                    ({
                        Header=header; 
                        Account=e.Account;
                        AccountLineage=getAccountLineage e.Account;
                        EntryType=e.EntryType;
                        Amount=e.Amount.Value;
                        Commodity=getValue e.CommodityValue e.Amount.Value;
                        Comment=e.Comment 
                    } : Entry)
                List.map toEntry es
            List.collect transactionToJournal ts


        /// Pipelined functions applied to the AST to produce the final journal data structure
        let transform = 
            transformASTToTransactions >> balanceTransactions >> toEntryList


    
    let private processResult result =
        match result with
            | Success(ast, _, _) -> PostProcess.transform ast
            | Failure(errorMsg, _, _) -> failwith errorMsg

    /// Run the parser against a file
    let parseJournalFile fileName encoding =
        runParserOnFile Parse.parseJournal () fileName encoding
        |> processResult

    /// Run the parser against a stream
    let parseJournalStream stream encoding =
        runParserOnStream Parse.parseJournal () "" stream encoding
        |> processResult



    // Parser unit tests

    module Test =
        open FsUnit.Xunit
        open Xunit
        open Parse

        let testParse parser text =
            match run parser text with
            | Success(result, _, _) -> Some(result)
            | Failure(_, _, _)      -> None

        [<Fact>]
        let ``skipWS ok on empty string`` () =
            testParse skipWS "" |> should equal (Some(()))
        
        [<Fact>]
        let ``skipWS ok when no space or tab`` () =
            testParse skipWS "alpha" |> should equal (Some(()))

        [<Fact>]
        let ``skipWS skips single space`` () =
            testParse skipWS " " |> should equal (Some(()))

        [<Fact>]
        let ``skipWS skips many spaces`` () =
            testParse skipWS "     " |> should equal (Some(()))

        [<Fact>]
        let ``skipWS skips single tab`` () =
            testParse skipWS "\t" |> should equal (Some(()))

        [<Fact>]
        let ``skipWS skips many tabs`` () =
            testParse skipWS "\t\t\t" |> should equal (Some(()))

        [<Fact>]
        let ``skipWS skips tabs and spaces`` () =
            testParse skipWS "   \t  \t \t " |> should equal (Some(()))