namespace WealthPulse

open WealthPulse.Journal
open WealthPulse.Journal.Query

module StaticRunner =

    type IndexLink = {
        LinkTitle: string;
        LinkURL: string;
    }

    type IndexData = {
        IndexLinks: IndexLink list;
    }

    type BalanceSheetRow = {
        Account: string;
        Balance: string;
        RowClass: string;
        BalanceClass: string;
        AccountStyle: string;
    }

    type BalanceSheetReportData = {
        Title: string;
        Subtitle: string;
        AccountBalances: BalanceSheetRow list;
        //NetWorthBookValue: string;
    }


    let layoutBalanceData (accountBalances, totalBalance) =
        let paddingLeftBase = 8
        let indentPadding = 20
        let getAccountDisplay account =
            let rec accountDisplay list (account: string) (parentage: string) indent =
                match list with
                | [] -> ((if parentage.Length > 0 then account.Remove(0, parentage.Length+1) else account), indent)
                | (acc, _) :: t when account.StartsWith(acc) && account <> acc && account.[acc.Length] = ':' -> accountDisplay t account acc (indent+1)
                | (acc, _) :: t -> accountDisplay t account parentage indent
            accountDisplay accountBalances account "" 0
        let accountBalances =
            List.sortBy fst accountBalances

        (accountBalances @ [("", totalBalance)])
        |> List.map (fun (account, (amount : decimal)) -> 
            let accountDisplay, indent = getAccountDisplay account
            { Account = accountDisplay; 
                Balance = amount.ToString("C"); 
                RowClass = (if account = "" then "grand_total" else ""); 
                BalanceClass = (if account = "" then "" elif amount >= 0M then "positive" else "negative"); 
                AccountStyle = (sprintf "padding-left: %dpx;" (paddingLeftBase+(indent*indentPadding))) })
        

    let generateAllReports journalData path =
        let renderReport (indexTitle: string) data templateFile outputFile =
            Nustache.Core.Render.FileToFile(templateFile, data, outputFile)
            { LinkTitle = indexTitle; LinkURL = outputFile }

        let templatesPath = path + @"templates\"
        let outputPath = path + @"output\"
        let (reportList: IndexLink list) = []
        
        let netWorthParameters = {
            AccountsWith = Some ["assets"; "liabilities"]; 
            ExcludeAccountsWith = Some ["units"]; 
            PeriodStart = None; 
            PeriodEnd = None;
        }
        let balanceSheetData = {
            Title = "Balance Sheet";
            Subtitle = "As of " + System.DateTime.Now.ToString("MMMM %d, yyyy");
            AccountBalances = layoutBalanceData <| Query.balance journalData netWorthParameters;
        }
        let reportList = (renderReport "Balance Sheet" balanceSheetData (templatesPath + "balance.html") (outputPath + "BalanceSheet.html")) :: reportList

        let currentMonthIncomeStatementParameters = {
            AccountsWith = Some ["income"; "expenses"]; 
            ExcludeAccountsWith = None; 
            PeriodStart = Some (DateUtils.getFirstOfMonth System.DateTime.Today); 
            PeriodEnd = Some (DateUtils.getLastOfMonth System.DateTime.Today);
        }
        let currentMonthIncomeStatementData = {
            Title = "Income Statement";
            Subtitle = "For period of " + currentMonthIncomeStatementParameters.PeriodStart.Value.ToString("MMMM %d, yyyy") + " to " + currentMonthIncomeStatementParameters.PeriodEnd.Value.ToString("MMMM %d, yyyy");
            AccountBalances = layoutBalanceData <| Query.balance journalData currentMonthIncomeStatementParameters;
        }
        let reportList = (renderReport "Income Statement - Current Month" currentMonthIncomeStatementData (templatesPath + "balance.html") (outputPath + "IncomeStatement-CurrentMonth.html")) :: reportList

        let previousMonthIncomeStatementParameters = {
            AccountsWith = Some ["income"; "expenses"]; 
            ExcludeAccountsWith = None; 
            PeriodStart = Some (DateUtils.getFirstOfMonth (System.DateTime.Today.AddMonths(-1))); 
            PeriodEnd = Some (DateUtils.getLastOfMonth (System.DateTime.Today.AddMonths(-1)));
        }
        let previousMonthIncomeStatementData = {
            Title = "Income Statement";
            Subtitle = "For period of " + previousMonthIncomeStatementParameters.PeriodStart.Value.ToString("MMMM %d, yyyy") + " to " + previousMonthIncomeStatementParameters.PeriodEnd.Value.ToString("MMMM %d, yyyy");
            AccountBalances = layoutBalanceData <| Query.balance journalData previousMonthIncomeStatementParameters;
        }
        let reportList = (renderReport "Income Statement - Previous Month" previousMonthIncomeStatementData (templatesPath + "balance.html") (outputPath + "IncomeStatement-PreviousMonth.html")) :: reportList

        let indexData = { IndexLinks = List.rev reportList }
        let reportList = renderReport "Index" indexData (templatesPath + "Index.html") (outputPath + "Index.html") :: reportList
    
        reportList


