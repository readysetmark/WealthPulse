namespace WealthPulse

open System

module Journal =

    // Contains all the type definitions in the Journal

    type Commodity = string

    type Amount = {
        Amount: decimal;
        Commodity: Commodity option;
    }

    type Status =
        | Cleared
        | Uncleared

    type EntryType =
        | Balanced
        | VirtualBalanced
        | VirtualUnbalanced

    type Header = {
        Date: System.DateTime;
        Status: Status;
        Code: string option;
        Description: string;
        Comment: string option
    }

    type Entry = {
        Header: Header;
        Account: string;
        AccountLineage: string list;
        EntryType: EntryType;
        Amount: Amount;
        Value: Amount option;
        Comment: string option;
    }

    type Journal = {
        Entries: Entry list;
        MainAccounts: Set<string>;
        AllAccounts: Set<string>;
        LastModified: DateTime;
    }


    /// Given a list of journal entries and last modified time, returns a Journal record
    let createJournal entries lastModified =
        let mainAccounts = Set.ofList <| List.map (fun (entry : Entry) -> entry.Account) entries
        let allAccounts = Set.ofList <| List.collect (fun entry -> entry.AccountLineage) entries
        { Entries=entries; MainAccounts=mainAccounts; AllAccounts=allAccounts; LastModified=lastModified; }
