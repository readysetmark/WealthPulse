/*****
  Sidebar Navigation
*****/

// ReportNav
//   @className
//   @title
//   @report
//   @query
var ReportNav = React.createClass({
  render: function() {
    var query = this.props.query.length > 0 ? "?" + this.props.query : "";
    var url = "#/" + this.props.report + query;
    return React.DOM.li({className: this.props.className},
                        React.DOM.a({href: url}, this.props.title));
  }
});


// PayeeNav
//   @className
//   @payee
//   @report
//   @query
//   @amountClass
//   @amount
var PayeeNav = React.createClass({
  render: function() {
    var query = this.props.query.length > 0 ? "?" + this.props.query : "";
    var url = "#/" + this.props.report + query;
    return React.DOM.li({className: this.props.className},
                        React.DOM.a({href: url},
                                    this.props.payee,
                                    React.DOM.span({className: "pull-right " + this.props.amountClass}, this.props.amount)));
  }
});


// NavBox
//   @reports
//   @payees
//   @journalLastModified
//   @report
//   @query
var NavBox = React.createClass({
  render: function() {
    var report_nodes = [];
    var payee_nodes = [];
    var i = 0;

    if (this.props.reports) {
      for (i = 0; i < this.props.reports.length; i++) {
        var report = this.props.reports[i];
        var className = null;
        if (report.report === this.props.report && report.query === this.props.query) {
          className = "active";
        }
        report_nodes.push(ReportNav({
          className: className,
          title: report.title,
          report: report.report,
          query: report.query,
          key: report.key
        }));
      }
    }

    if (this.props.payees) {
      for (i = 0; i < this.props.payees.length; i++) {
        var payee = this.props.payees[i];
        var className = null;
        if (payee.command.report === this.props.report && decodeURIComponent(payee.command.query) === this.props.query) {
          className = "active";
        }
        payee_nodes.push(PayeeNav({className: className,
                                   payee: payee.payee,
                                   amountClass: payee.amountClass,
                                   amount: payee.amount,
                                   report: payee.command.report,
                                   query: payee.command.query,
                                   key: payee.payee}));
      }
    }

    return React.DOM.div(null,
      React.DOM.div({
          className: "well",
          style: {
            padding: "8px 0"
          }
        },
        React.DOM.ul({className: "nav nav-list"},
          React.DOM.li({className: "nav-header"}, "Reports"),
          report_nodes,
          React.DOM.li({className: "nav-header"}, "Payables / Receivables"),
          payee_nodes,
          React.DOM.li({className: "divider"}),
          React.DOM.li({className: "nav-header"}, "Last Modified"),
          React.DOM.li({className: "muted"}, this.props.journalLastModified))
      )
    );
  }
});



/*****
  Balance Report
*****/

// BalanceReportRow
//   @rowClass
//   @balanceClass
//   @balance
//   @accountStyle
//   @account
//   @realBalance
//   @commodityBalance
//   @price
//   @priceDate
//   @showCommodities
var BalanceReportRow = React.createClass({
  render: function() {
    var link = "#/register?accountsWith=" + encodeURIComponent(this.props.key);
    var commodity_columns = [];

    if (this.props.showCommodities) {
      commodity_columns = [React.DOM.td({className: "currency "+ this.props.balanceClass}, this.props.realBalance),
                           React.DOM.td({className: "currency"}, this.props.commodityBalance),
                           React.DOM.td({className: "currency"}, this.props.price),
                           React.DOM.td(null, this.props.priceDate)]
    }

    var row = React.DOM.tr({className: this.props.rowClass},
                           React.DOM.td({style: this.props.accountStyle},
                                        React.DOM.a({href: link}, this.props.account)),
                           React.DOM.td({className: "currency "+ this.props.balanceClass}, this.props.balance.join(" ")),
                           commodity_columns
                           );
    return row;
  }
});


// BalanceReport
//   @title
//   @subtitle
//   @balances
var BalanceReport = React.createClass({
  render: function() {
    var table_rows = [];
    var commodity_headers = [];
    var show_commodities = false;
    var table_span = "span4"
    var i = 0;

    // determine if we must show the commodity-related columns
    if (this.props.hasOwnProperty('balances')) {
      for (i = 0; i < this.props.balances.length; i++) {
        var balance = this.props.balances[i];
        if (balance.realBalance != "") {
          show_commodities = true;
          table_span = "span10";
          commodity_headers = [React.DOM.th(null, "Real Balance"),
                               React.DOM.th(null, "Commodity"),
                               React.DOM.th(null, "Price"),
                               React.DOM.th(null, "Price Date")];
        }
      }
    }

    // generate table rows
    if (this.props.hasOwnProperty('balances')) {
      for (i = 0; i < this.props.balances.length; i++) {
        var balance = this.props.balances[i];
        balance.showCommodities = show_commodities;
        table_rows.push(BalanceReportRow(balance));
      }
    }

    var header = React.DOM.header({className: "page-header"},
                                  React.DOM.h1(null,
                                               this.props.title,
                                               React.DOM.br(),
                                               React.DOM.small(null, this.props.subtitle)));
    var body = React.DOM.section({className: table_span},
                                 React.DOM.table({className: "table table-hover table-condensed"},
                                                 React.DOM.thead(null,
                                                                 React.DOM.tr(null,
                                                                              React.DOM.th(null, "Account"),
                                                                              React.DOM.th(null, "Balance"),
                                                                              commodity_headers)),
                                                 React.DOM.tbody(null, table_rows)));

    return React.DOM.div(null, header, body);
  }
});



/*****
  Register Report
*****/

// RegisterReportRow
//   @cellClass
//   @date
//   @description
//   @account
//   @amount
//   @total
var RegisterReportRow = React.createClass({
  render: function() {
    var link = "#/register?accountsWith=" + encodeURIComponent(this.props.account);
    var row = React.DOM.tr(null,
                           React.DOM.td({className: this.props.cellClass}, this.props.date),
                           React.DOM.td({className: this.props.cellClass}, this.props.description),
                           React.DOM.td({className: this.props.cellClass}, React.DOM.a({href: link}, this.props.account)),
                           React.DOM.td({className: "currency " + this.props.cellClass}, this.props.amount),
                           React.DOM.td({className: "currency " + this.props.cellClass}, this.props.total));
    return row;
  }
});


// RegisterReport
//   @title
//   @subtitle
//   @register
var RegisterReport = React.createClass({
  render: function() {
    var table_rows = [];
    var i = 0;
    var j = 0;

    if (this.props.hasOwnProperty('register')) {
      for (i = 0; i < this.props.register.length; i++) {
        var transaction = this.props.register[i];
        for (j = 0; j < transaction.entries.length; j++) {
          var data = {
            key: transaction.date +"~"+
                 transaction.payee +"~"+
                 transaction.entries[j].account +"~"+
                 transaction.entries[j].amount,
            account: transaction.entries[j].account,
            amount: transaction.entries[j].amount,
            total: transaction.entries[j].total
          };
          if (j === 0) {
            data.date = transaction.date;
            data.description = transaction.payee;
          }
          else {
            data.cellClass = "no-border-top";
          }
          table_rows.push(RegisterReportRow(data));
        }
      }
    }

    var header = React.DOM.header({className: "page-header"},
                                  React.DOM.h1(null,
                                               this.props.title,
                                               React.DOM.br(),
                                               React.DOM.small(null, this.props.subtitle)));
    var body = React.DOM.section({className: "span10"},
                                 React.DOM.table({className: "table table-hover table-condensed"},
                                                 React.DOM.thead(null,
                                                                 React.DOM.tr(null,
                                                                              React.DOM.th({style: {"min-width": "75px"}}, "Date"),
                                                                              React.DOM.th(null, "Description"),
                                                                              React.DOM.th(null, "Account"),
                                                                              React.DOM.th(null, "Amount"),
                                                                              React.DOM.th(null, "Total"))),
                                                 React.DOM.tbody(null, table_rows)));

    return React.DOM.div(null, header, body);
  }
});



/*****
  Networth Report
*****/

// NetworthReport
//   @title
//   @data
var NetworthReport = React.createClass({
  componentDidMount: function (root) {
    var margin = {top: 20, right: 20, bottom: 30, left: 55},
        width = 600 - margin.left - margin.right,
        height = 400 - margin.top - margin.bottom;

    var parseDate = d3.time.format("%d-%b-%Y").parse;

    var x = d3.time.scale()
    .range([0, width]);

    var y = d3.scale.linear()
    .range([height, 0]);

    var xAxis = d3.svg.axis()
    .scale(x)
    .ticks(d3.time.months, 3)
    .tickFormat(d3.time.format("%b %y"))
    .orient("bottom");

    var yAxis = d3.svg.axis()
    .scale(y)
    .orient("left");

    var line = d3.svg.line()
    .x(function(d) { return x(d.date); })
    .y(function(d) { return y(d.amount); });

    var svg = d3.select('#linechart').append("svg")
    .attr("width", width + margin.left + margin.right)
    .attr("height", height + margin.top + margin.bottom)
    .append("g")
    .attr("transform", "translate(" + margin.left + "," + margin.top + ")");

    if (!this.props.data) {
      return;
    }

    this.props.data.forEach(function(d) {
      d.date = parseDate(d.date);
      d.amount = parseFloat(d.amount);
    });

    x.domain(d3.extent(this.props.data, function(d) { return d.date; }))
    .nice(d3.time.month);
    y.domain(d3.extent(this.props.data, function(d) { return d.amount; }));

    svg.append("g")
    .attr("class", "x axis")
    .attr("transform", "translate(0," + height + ")")
    .call(xAxis);

    svg.append("g")
    .attr("class", "y axis")
    .call(yAxis)
    .append("text")
    .attr("transform", "rotate(-90)")
    .attr("y", 6)
    .attr("dy", ".71em")
    .style("text-anchor", "end")
    .text("Amount ($)");

    svg.append("path")
    .datum(this.props.data)
    .attr("class", "line")
    .attr("d", line);

    var node = svg.append("g")
    .attr("class", "nodes")
    .selectAll("circle")
    .data(this.props.data)
    .enter()
    .append("circle")
    .attr("class", "node")
    .attr("r", 2)
    .attr("cx", function(d) { return x(d.date); })
    .attr("cy", function(d) { return y(d.amount); });

    var hover = svg.append("g")
    .attr("class", "node-hover")
    .style("display", "block")
    .style("visibility", "hidden");

    var hover_rect = hover.append("rect")
    .attr("class", "node-hover-rect")
    .attr("rx", "5")
    .attr("ry", "5")
    .attr("width", "120")
    .attr("height", "30");

    var hover_text = hover.append("text")
    .attr("class", "node-hover-text")
    .attr("transform", "translate(10,20)");

    node
    .on("mouseover", function(d) {
      var text_width = 120;
      var lines = 1;

      hover_text.selectAll("tspan").remove();
      var name = hover_text.append("tspan")
      .attr("class", "node-hover-text-name")
      .attr("x", "0")
      .text(d.hover);

      text_width = name.node().getComputedTextLength();

      hover_rect
      .attr("width", text_width + 20);

      var translate_x = x(d.date) + 10;
      if (translate_x + text_width + 20 > width) {
        translate_x = x(d.date) - 30 - text_width;
      }

      var translate_y = y(d.amount) + 10;
      if (translate_y + 30 > height) {
        translate_y = y(d.amount) - 40;
      }

      hover
      .attr("transform", "translate(" + translate_x + ", " + translate_y +")")
      .style("visibility", "visible");
    })
    .on("mouseout", function(d) {
      hover.style("visibility", "hidden");
    })
  },
  render: function () {
    var header = React.DOM.header({className: "page-header"},
                                  React.DOM.h1(null, this.props.title));

    var body = React.DOM.section({id: 'linechart'});

    return React.DOM.div(null, header, body);
  }
});


/*****
  Exception Box
*****/

// ExceptionBox
//   @dismiss - function
//   @exceptionMessage
var ExceptionBox = React.createClass({
  render: function () {
    var display = this.props.exceptionMessage ? "block" : "none";
    var div = React.DOM.div({className: "alert alert-error",
                             style: {display: display}},
                            React.DOM.button({type: "button",
                                              className: "close",
                                              onClick: this.props.dismiss},
                                             "\u00D7"),
                            this.props.exceptionMessage);

    return div;
  }
});



/*****
  Routes
*****/

var WealthPulseRouter = Backbone.Router.extend({
  routes: {
    '': 'home',
    'balance(?*query)': 'balance',
    'register(?*query)': 'register',
    'networth': 'networth'
  }
});



/*****
  App Component
*****/

var WealthPulseApp = React.createClass({
  getInitialState: function() {
    return {navData: {}, report: "", query: "", reportData: {}, exceptionMessage: null};
  },
  componentWillMount: function () {
    var that = this;
    this.router = new WealthPulseRouter();
    this.router.on('route:home', this.home);
    this.router.on('route:balance', this.balance);
    this.router.on('route:register', this.register);
    this.router.on('route:networth', this.networth);
  },
  componentDidMount: function () {
    var self = this;
    Backbone.history.start();

    // bind '/' as hotkey for command bar
    $(document).bind('keyup', '/', function () {
      $("#command").focus().select();
    });

    // do not submit form when <enter> pressed
    $("#command").keypress(function(e) {
      if (e.which == 13) {
        e.preventDefault();
        self.processCommand();
      }
    });

    $("#submit").click(function(e) {
      e.preventDefault();
      self.processCommand();
      return false;
    });
  },

  // Routes
  home: function () {
    var defaultReport = 'balance';
    var defaultQuery = 'accountsWith=assets+liabilities&excludeAccountsWith=units';
    //console.log('home');
    this.loadData(defaultReport, defaultQuery);
  },
  balance: function (query) {
    //console.log('balance with query='+ query);
    this.loadData('balance', query);
  },
  register: function (query) {
    //console.log('register with query='+ query);
    this.loadData('register', query);
  },
  networth: function () {
    //console.log('networth');
    this.loadData('networth');
  },

  // Data Fetching
  loadData: function (report, query) {
    var self = this;
    $.when(this.loadNav(), this.loadReport(report, query))
      .done(function (navArgs, reportArgs) {
        //console.log("ajax done.");
        var newException = navArgs[0].exceptionMessage;
        self.setState({
          navData: navArgs[0],
          report: report,
          query: query ? query : "",
          reportData: reportArgs[0],
          exceptionMessage: newException ? newException : self.state.exceptionMessage
        });
      });
  },
  loadNav: function () {
    return $.ajax({
      url: 'api/nav',
      dataType: 'json'
    });
  },
  loadReport: function (report, query) {
    return $.ajax({
      url: 'api/' + report + (query ? "?" + query : ""),
      dataType: 'json',
    });
  },

  // Command bar
  determineReport: function (token) {
    switch (token) {
      case 'bal':
      case 'balance':
        return 'balance';
      case 'reg':
      case 'register':
        return 'register';
      case 'nw':
      case 'networth':
        return 'networth';
      default:
        throw new Error("Unable to determine report.");
    }
  },
  parseParameters: function (tokens) {
    var collect = function (state, value) {
      if (value[0] === ':') {
        // keyword changes parse mode
        var newMode = value.substring(1);
        if (newMode === "exclude") {
          newMode = "excludeAccountsWith";
        }
        state.mode = newMode;
      }
      else {
        // collect values in current mode
        if (state.parameters.hasOwnProperty(state.mode)) {
          state.parameters[state.mode].push(encodeURIComponent(value));
        }
        else {
          state.parameters[state.mode] = [value];
        }
      }

      return state;
    };

    return _.reduce(tokens, collect, {mode: 'accountsWith', parameters: {}}).parameters;
  },
  parametersToQueryString: function (parameters) {
    var collect = function(state, value, key) {
      if (state.length > 0) {
        state += "&";
      }
      state += key + "=" + value.join("+");
      return state;
    };

    return _.reduce(parameters, collect, "");
  },
  processCommand: function () {
    var command = $("#command").val();

    if (command.length > 0) {
      var tokens = command.toLowerCase().split(" ");
      var report = this.determineReport(tokens[0]);
      tokens.shift();
      var parameters = this.parseParameters(tokens);
      var query = this.parametersToQueryString(parameters);
      var url = '#/' + report + (query.length > 0 ? "?" + query : "");
      //console.log(url);
      this.router.navigate(url, {trigger: true});
    }
  },

  render: function() {
    var self = this;
    var report = null;
    console.log("this.state:");
    console.log(this.state);
    var navBox = NavBox({
      reports: this.state.navData.reports,
      payees: this.state.navData.payees,
      journalLastModified: this.state.navData.journalLastModified,
      report: this.state.report,
      query: this.state.query
    });
    var exceptionBox = ExceptionBox({
      dismiss: function () {
        var newState = {
          navData: self.state.navData,
          report: self.state.report,
          query: self.state.query,
          reportData: self.state.reportData,
          exceptionMessage: null
        }
        self.setState(newState);
        //console.log("dismissed!");
      },
      exceptionMessage: this.state.exceptionMessage
    });

    //console.log("will render: "+ this.state.report);
    switch (this.state.report) {
      case 'balance':
        report = BalanceReport(this.state.reportData);
        break;
      case 'register':
        report = RegisterReport(this.state.reportData);
        break;
      case 'networth':
        report = NetworthReport(this.state.reportData);
        break;
    }

    var header = React.DOM.div({className: "navbar navbar-inverse navbar-fixed-top"},
                               React.DOM.div({className: "navbar-inner"},
                                             React.DOM.div({className: "container-fluid"},
                                                           React.DOM.a({className: "brand", href: "/"},
                                                                       "Wealth Pulse"),
                                                           React.DOM.form({className: "navbar-form pull-right", name: "command"},
                                                                          React.DOM.input({type: "text",
                                                                                           id: "command",
                                                                                           name: "command",
                                                                                           className: "span8",
                                                                                           placeholder: "Command"}),
                                                                          React.DOM.button({type: "button",
                                                                                            id: "submit",
                                                                                            className: "btn btn-primary"},
                                                                                           "Submit")))));

    var body = React.DOM.div({className: "container-fluid"},
                             React.DOM.div({className: "row-fluid"},
                                           React.DOM.nav({className: "span2"}, navBox),
                                           React.DOM.section({className: "span10"},
                                                             exceptionBox,
                                                             report)));

    return React.DOM.div({id: "app"}, header, body);
  }
});



/*****
  Initialization
*****/

React.renderComponent(
  WealthPulseApp({}),
  document.body
);
