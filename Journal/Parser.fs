namespace WealthPulse.Journal

open FParsec

// Parser module contains functions for parsing the Ledger journal file

module Parser =
    
    // AST module contains transient types for the Abstract Syntax Tree used by the parser
    // Eventually we convert all the data to the types in the Journal module

    module private AST =

        type ValueSpecification =
            | TotalCost of Amount
            | UnitCost of Amount

        type TransactionHeader = {
            Date: System.DateTime;
            Status: Status;
            Code: string option;
            Description: string;
            Comment: string option
            }

        type TransactionEntry = {
            mutable Account: string;
            mutable EntryType: EntryType option;
            mutable Amount: Amount option;
            Value: ValueSpecification option;
            Comment: string option
            }

        type AST =
            | Comment of string
            | Transaction of TransactionHeader * AST list
            | Entry of TransactionEntry


    
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
            stringsSepEndBy (many1Chars accountChar) (pstring " ") .>> skipWS

        /// Parse the numeric portion of an amount
        let parseAmountNumber =
            let isAmountChar c = isDigit c || c = '.' || c = ','
            attempt (pipe2 <| pchar '-' <| many1Satisfy isAmountChar <| (fun a b -> a.ToString() + b)) 
            <|> (many1Satisfy isAmountChar)
            .>> skipWS
            |>> System.Decimal.Parse

        /// Parse the commodity portion of an amount
        let parseCommodity =
            let quotedIdentifier = noneOf "\r\n\""
            let identifier = noneOf "-0123456789., @;\r\n\""
            attempt (pchar '\"' >>. manyCharsTill quotedIdentifier (pchar '\"'))
            <|> attempt (many1Chars identifier)
            .>> skipWS

        /// Parse an amount that includes the numerical value and the commodity.
        /// The commodity can come before or after the amount. If the commodity contains
        /// numbers or a space, it must be quoted.
        let parseAmount =
            let amountTuple amount = (amount, None)
            let reverse (a,b) = (b,a)
            attempt (parseAmountNumber .>>. (parseCommodity |>> Some))
            <|> attempt (parseAmountNumber |>> amountTuple)
            <|> ((parseCommodity |>> Some) .>>. parseAmountNumber |>> reverse)

        /// Parse a value amount in terms of UnitCost or TotalCost
        let parseValue =
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
            let createEntry account amount value comment =
                {Account=account; EntryType=None; Amount=amount; Value=value; Comment=comment}
            pipe4 parseAccount (opt parseAmount) (opt parseValue) (opt parseComment) createEntry |>> Entry

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

    
    
    // Module PostProcess contains post-parsing transformations

    module private PostProcess =
        open AST

        /// Transforms the AST data structure into a list of (TransactionHeader, TransactionEntry list) tuples
        /// Basically, we're dropping all the comment nodes
        let transformASTToTransactions ast =
            let transactionFilter (e: AST) =
                match e with
                | Transaction(_,_) -> true
                | _ -> false

            let getTransactionHeader (e: AST) =
                match e with
                | Transaction(header, ast) -> (header, ast)
                | _ -> failwith "Unexpected AST value in getTransactionHeader"

            let transactionEntryFilter (e: AST) =
                match e with
                | Entry(_) -> true
                | _ -> false

            let getTransactionEntry (e: AST) =
                match e with
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


        /// Perform multiple mutations on the transaction entries
        let processTransactions ts =
            /// Determines and sets the EntryType for each transaction and updates the account
            let determineEntryType (h, es) =
                let getEntryType (account: string) =
                        match account.[0] with
                        | '(' -> VirtualUnbalanced
                        | '[' -> VirtualBalanced
                        | _ -> Balanced

                for entry in es do
                    let entryType = getEntryType entry.Account
                    let account = (entry.Account.TrimStart [| '('; '[' |]).TrimEnd [| ')'; ']' |]
                    entry.Account <- account
                    entry.EntryType <- Some entryType
                (h, es)

            /// Auto-balances all transaction entries per transaction header
            /// This method is still not correct as it does not take into account:
            ///     entry type: normal/virtual balanced/virtual unbalanced
            ///     if different commodities are used
            let autobalanceTransaction (h, es) =
                let mutable balance = 0M
                let mutable commodity : Commodity option = None
                let mutable entryMissingAmount : TransactionEntry ref option = None
                for entry in es do
                    balance <- if entry.Amount.IsSome then balance + (fst entry.Amount.Value) else balance
                    commodity <- if commodity.IsNone && entry.Amount.IsSome && (snd entry.Amount.Value).IsSome then (snd entry.Amount.Value) else commodity
                    entryMissingAmount <- if entryMissingAmount.IsNone && entry.Amount.IsNone then Some (ref entry) else entryMissingAmount
                if entryMissingAmount.IsSome then (!entryMissingAmount.Value).Amount <- Some (-1M * balance, commodity)
                (h, es)

            let endPipeline (_, _) = ()
            let transformPipeline =
                determineEntryType >> autobalanceTransaction >> endPipeline
            List.iter transformPipeline ts
            ts


        /// Convert to a list of journal entries (transaction entries)
        let toJournal ts =
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
                    let getValue (value: ValueSpecification option) (amount, c) =
                        match value with
                        | Some(TotalCost(v)) -> Some v
                        | Some(UnitCost((a, commodity))) -> Some (a * amount, commodity)
                        | None -> None
                    ({ Header=header; Account=e.Account; AccountLineage=getAccountLineage e.Account; EntryType=e.EntryType.Value; Amount=e.Amount.Value; Value=getValue e.Value e.Amount.Value; Comment=e.Comment } : Entry)
                List.map toEntry es
            List.collect transactionToJournal ts

        /// Returns journal data containing all transactions, a list of main accounts and a list of all accounts that includes parent accounts
        let getJournalData (entries : Entry list) =
            let mainAccounts = Set.ofList <| List.map (fun (entry : Entry) -> entry.Account) entries
            let allAccounts = Set.ofList <| List.collect (fun entry -> entry.AccountLineage) entries
            { Transactions=entries; MainAccounts=mainAccounts; AllAccounts=allAccounts }
 
        /// Pipelined functions applied to the AST to produce the final journal data structure
        let transformPipeline = 
            transformASTToTransactions >> processTransactions >> toJournal >> getJournalData


    
    let private processResult result =
        match result with
            | Success(ast, _, _) -> ast |> PostProcess.transformPipeline
            | Failure(errorMsg, _, _) -> failwith errorMsg

    /// Run the parser against a file
    let parseJournalFile fileName encoding =
        runParserOnFile Parse.parseJournal () fileName encoding
        |> processResult

    /// Run the parser against a stream
    let parseJournalStream stream encoding =
        runParserOnStream Parse.parseJournal () "" stream encoding
        |> processResult

