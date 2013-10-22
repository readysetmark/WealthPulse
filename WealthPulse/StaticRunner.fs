namespace WealthPulse

open WealthPulse.Journal

module StaticRunner =

    type IndexLink = {
        LinkTitle: string;
        LinkURL: string;
    }

    type IndexData = {
        IndexLinks: IndexLink list;
    }

    let generateAllReports journalData path =
        let renderReport (indexTitle: string) dataFunction templateFile outputFile =
            Nustache.Core.Render.FileToFile(templateFile, dataFunction(), outputFile)
            { LinkTitle = indexTitle; LinkURL = outputFile }

        let templatesPath = path + @"templates\"
        let outputPath = path + @"output\"
        let (reportList: IndexLink list) = []

        let balanceSheetData = (fun () -> Query.generateBalanceReportData journalData System.DateTime.Today)
        let reportList = (renderReport "Balance Sheet" balanceSheetData (templatesPath + "BalanceSheet.html") (outputPath + "BalanceSheet.html")) :: reportList

        let currentMonthIncomeStatementData = (fun () -> Query.generateIncomeReportData journalData (DateUtils.getFirstOfMonth System.DateTime.Today) (DateUtils.getLastOfMonth System.DateTime.Today))
        let reportList = (renderReport "Income Statement - Current Month" currentMonthIncomeStatementData (templatesPath + "IncomeStatement.html") (outputPath + "IncomeStatement-CurrentMonth.html")) :: reportList

        let previousMonthIncomeStatementData = (fun () -> Query.generateIncomeReportData journalData (DateUtils.getFirstOfMonth (System.DateTime.Today.AddMonths(-1))) (DateUtils.getLastOfMonth (System.DateTime.Today.AddMonths(-1))))
        let reportList = (renderReport "Income Statement - Previous Month" previousMonthIncomeStatementData (templatesPath + "IncomeStatement.html") (outputPath + "IncomeStatement-PreviousMonth.html")) :: reportList

        let indexData = (fun () -> { IndexLinks = List.rev reportList })
        let reportList = renderReport "Index" indexData (templatesPath + "Index.html") (outputPath + "Index.html") :: reportList
    
        reportList


