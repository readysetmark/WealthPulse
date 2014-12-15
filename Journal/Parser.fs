namespace Journal

open FParsec
open Journal.Types

/// Parser module contains functions for parsing the Ledger journal file
module Parser =

    /// Contains transient types used during parsing. Eventually all the data
    /// will be converted to the appropriate Journal types.
    module Types =

        /// Parsed Entry. The only difference from a Journal.Type.Entry is the Amount field is an option,
        /// to allow for entries with blank amounts during parsing. A blank will get calculated when we
        /// create the Journal.    
        type ParsedEntry = {
            Account: string;
            EntryType: EntryType;
            Amount: Amount option;
            Comment: string option
        }

        /// A parsed line will be one of these types
        type ParsedLine =
            | Comment of string
            | Price of SymbolPrice
            | Transaction of Header * ParsedLine list
            | Entry of ParsedEntry


    /// Parser utility functions
    module Utilities =

        /// Call Trim() on a string
        let trim (s : string) = 
            s.Trim()


    /// Terminals for parser
    module Terminals =

        /// Whitespace characters        
        let whitespace c =
            c = ' ' || c = '\t'
        
        /// Date separator
        let dateSeparator c =
            c = '/' || c = '-'
    

    /// Parsing combinator functions
    module Combinators =
        open Types
        open Utilities
        open Terminals
        
        /// Skip whitespace as spaces and tabs
        let skipWS = 
            skipManySatisfy whitespace

        /// Parse a date
        let parseDate =
            let isDateChar c = isDigit c || dateSeparator c
            many1SatisfyL isDateChar "Expecting a date separated by / or -" .>> skipWS |>> System.DateTime.Parse

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

        /// Parse a complete transaction header
        let parseTransactionHeader =
            let createHeader date status code payee comment =
                {Date=date; Status=status; Code=code; Description=payee; Comment=comment}        
            pipe5 parseDate parseTransactionStatus (opt parseCode) parsePayee (opt parseComment) createHeader

        /// Parse a complete transaction entry
        let parseTransactionEntry =
            let createEntry (account, entryType) amount comment =
                {Account=account; EntryType=entryType; Amount=amount; Comment=comment}
            pipe3 parseAccount (opt parseAmount) (opt parseComment) createEntry |>> Entry

        /// Parse a journal comment line
        let parseCommentLine = parseComment |>> Comment

        /// Parse a complete transaction
        let parseTransaction =
            let parseEntry = attempt (skipWS >>. (parseTransactionEntry <|> parseCommentLine) .>> newline)
            parseTransactionHeader .>> newline
            .>>. many parseEntry
            |>> Transaction

        /// Parse a price entry. e.g. "P 2014/12/14 AAPL $23.44"
        let parsePrice =
            let parseP = pchar 'P' .>> skipWS
            parseP >>. pipe3 parseDate parseSymbol parseAmount SymbolPrice.create |>> Price

        /// Parse a complete ledger journal
        let parseJournal =
            sepEndBy (parseCommentLine <|> parseTransaction <|> parsePrice) (many (skipWS >>. newline))


        
    // Module PostProcess contains post-parsing transformations

    module private PostProcess =
        open Types

        /// Transforms the ParsedLine tree data structure into a list of (Header, ParsedEntry list) tuples
        /// Basically, we're dropping all the comment nodes
        let transformParsedLinesToTransactions lines =
            let transactionFilter (line: ParsedLine) =
                match line with
                | Transaction(_,_) -> true
                | _ -> false

            let getTransactionHeader (line: ParsedLine) =
                match line with
                | Transaction(header, lines) -> (header, lines)
                | _ -> failwith "Unexpected AST value in getTransactionHeader"

            let transactionEntryFilter (line: ParsedLine) =
                match line with
                | Entry(_) -> true
                | _ -> false

            let getTransactionEntry (line: ParsedLine) =
                match line with
                | Entry(te) -> te
                | _ -> failwith "Unexpected AST value in getTransactionEntry"

            let getTransactionEntries (header, lines) =
                let entries = 
                    lines
                    |> List.filter transactionEntryFilter
                    |> List.map getTransactionEntry
                (header, entries)

            lines
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
                    ({
                        Header=header; 
                        Account=e.Account;
                        AccountLineage=Account.getAccountLineage e.Account;
                        EntryType=e.EntryType;
                        Amount=e.Amount.Value;
                        Comment=e.Comment 
                    } : Entry)
                List.map toEntry es
            List.collect transactionToJournal ts


        /// Pipelined functions applied to the AST to produce the final journal data structure
        let transform = 
            transformParsedLinesToTransactions >> balanceTransactions >> toEntryList


    
    let private processResult result =
        match result with
            | Success(ast, _, _) -> PostProcess.transform ast
            | Failure(errorMsg, _, _) -> failwith errorMsg

    /// Run the parser against a file
    let parseJournalFile fileName encoding =
        runParserOnFile Combinators.parseJournal () fileName encoding
        |> processResult

    /// Run the parser against a stream
    let parseJournalStream stream encoding =
        runParserOnStream Combinators.parseJournal () "" stream encoding
        |> processResult
