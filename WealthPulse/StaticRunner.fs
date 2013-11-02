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
    }

    type LineChartPoint = {
        X: string;
        Y: string;
        Hover: string;
    }

    type LineChartReportData = {
        Title: string;
        //Data: LineChartPoint list;
        Data: string;
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
    

    let generateNetWorthData journalData =
        let generatePeriodBalance month =
            let parameters = {
                AccountsWith = Some ["assets"; "liabilities"];
                ExcludeAccountsWith = Some ["units"];
                PeriodStart = None;
                PeriodEnd = Some (DateUtils.getLastOfMonth(month));
            }
            let _, totalBalance = Query.balance parameters journalData
            {
                X = month.ToString("dd-MMM-yyyy"); 
                Y = totalBalance.ToString(); 
                Hover = month.ToString("MMM yyyy") + ": " + totalBalance.ToString("C");
            }

        let firstMonth = DateUtils.getFirstOfMonth(System.DateTime.Today).AddMonths(-25)
        let months = seq { for i in 0 .. 25 do yield firstMonth.AddMonths(i) }

        let netWorthData =
            months
            |> Seq.map generatePeriodBalance
            |> Seq.toList

        let jsonSerializer = new System.Web.Script.Serialization.JavaScriptSerializer()
        jsonSerializer.Serialize(netWorthData)

    let generateAllReports journalData path =
        let renderReport (indexTitle: string) data templateFile outputFile =
            Nustache.Core.Render.FileToFile(templateFile, data, outputFile)
            { LinkTitle = indexTitle; LinkURL = outputFile }

        let templatesPath = path + @"templates\"
        let outputPath = path + @"output\"
        let (reportList: IndexLink list) = []
        
        let balanceSheetParameters = {
            AccountsWith = Some ["assets"; "liabilities"]; 
            ExcludeAccountsWith = Some ["units"]; 
            PeriodStart = None; 
            PeriodEnd = None;
        }
        let balanceSheetData = {
            Title = "Balance Sheet";
            Subtitle = "As of " + System.DateTime.Now.ToString("MMMM %d, yyyy");
            AccountBalances = layoutBalanceData <| Query.balance balanceSheetParameters journalData;
        }
        let reportList = (renderReport "Balance Sheet" balanceSheetData (templatesPath + "balance.html") (outputPath + "BalanceSheet.html")) :: reportList

        let netWorthData = {
            Title = "Net Worth";
            Data = generateNetWorthData journalData;
        }
        let reportList = (renderReport "Net Worth" netWorthData (templatesPath + "linechart.html") (outputPath + "NetWorth.html")) :: reportList

        let currentMonthIncomeStatementParameters = {
            AccountsWith = Some ["income"; "expenses"]; 
            ExcludeAccountsWith = None; 
            PeriodStart = Some (DateUtils.getFirstOfMonth System.DateTime.Today); 
            PeriodEnd = Some (DateUtils.getLastOfMonth System.DateTime.Today);
        }
        let currentMonthIncomeStatementData = {
            Title = "Income Statement";
            Subtitle = "For period of " + currentMonthIncomeStatementParameters.PeriodStart.Value.ToString("MMMM %d, yyyy") + " to " + currentMonthIncomeStatementParameters.PeriodEnd.Value.ToString("MMMM %d, yyyy");
            AccountBalances = layoutBalanceData <| Query.balance currentMonthIncomeStatementParameters journalData;
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
            AccountBalances = layoutBalanceData <| Query.balance previousMonthIncomeStatementParameters journalData;
        }
        let reportList = (renderReport "Income Statement - Previous Month" previousMonthIncomeStatementData (templatesPath + "balance.html") (outputPath + "IncomeStatement-PreviousMonth.html")) :: reportList

        let indexData = { IndexLinks = List.rev reportList }
        let reportList = renderReport "Index" indexData (templatesPath + "index.html") (outputPath + "Index.html") :: reportList
    
        reportList


